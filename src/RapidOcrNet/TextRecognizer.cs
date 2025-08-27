// Apache-2.0 license
// Adapted from RapidAI / RapidOCR
// https://github.com/RapidAI/RapidOCR/blob/92aec2c1234597fa9c3c270efd2600c83feecd8d/dotnet/RapidOcrOnnxCs/OcrLib/CrnnNet.cs

using System.Diagnostics;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SkiaSharp;

namespace RapidOcrNet
{
    public sealed class TextRecognizer : IDisposable
    {
        private readonly float[] MeanValues = [127.5F, 127.5F, 127.5F];
        private readonly float[] NormValues = [1.0F / 127.5F, 1.0F / 127.5F, 1.0F / 127.5F];
        private const int CrnnDstHeight = 48;
        //private const int CrnnCols = 6625;

        private InferenceSession _crnnNet = null!;
        private string[] _keys = null!;
        private string _inputName = null!;
        public int LabelCount { get; private set; }
        public int ModelClassCount { get; private set; }

        public void InitModel(string path, int numThread)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Recognizer model file does not exist: '{path}'.");
            }

            var op = new SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_EXTENDED,
                InterOpNumThreads = numThread,
                IntraOpNumThreads = numThread,
                LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR
            };

            _crnnNet = new InferenceSession(path, op);
            _inputName = _crnnNet.InputMetadata.Keys.First();

            var metaMap = _crnnNet.ModelMetadata?.CustomMetadataMap;
            if (metaMap == null || !metaMap.TryGetValue("character", out var chars))
            {
                throw new InvalidOperationException("Recognizer model missing 'character' metadata.");
            }

            var lines = chars.Split('\n');
            var keys = new List<string> { "#" };
            keys.AddRange(lines);
            keys.Add(" ");
            _keys = keys.ToArray();
            var meta = _crnnNet.OutputMetadata.First().Value;
            ModelClassCount = meta.Dimensions[^1];
            LabelCount = _keys.Length - 1; // dictionary lines without CTC blank
            if (ModelClassCount != _keys.Length)
            {
                if (ModelClassCount > _keys.Length)
                {
                    var pad = ModelClassCount - _keys.Length;
                    _keys = _keys.Concat(Enumerable.Repeat("?", pad)).ToArray();
                }
                else
                {
                    _keys = _keys.Take(ModelClassCount).ToArray();
                }
                LabelCount = _keys.Length - 1;
            }
        }


        public TextLine[] GetTextLines(SKBitmap[] partImgs)
        {
            var textLines = new TextLine[partImgs.Length];
            for (int i = 0; i < partImgs.Length; i++)
            {
                textLines[i] = GetTextLine(partImgs[i]);
            }

            return textLines;
        }

        public TextLine GetTextLine(SKBitmap src)
        {
            var sw = Stopwatch.StartNew();
            float scale = CrnnDstHeight / (float)src.Height;
            int dstWidth = (int)(src.Width * scale);

            Tensor<float> inputTensors;
            using (SKBitmap srcResize = src.Resize(new SKSizeI(dstWidth, CrnnDstHeight), new SKSamplingOptions(SKFilterMode.Linear)))
            {
                inputTensors = OcrUtils.SubtractMeanNormalize(srcResize, MeanValues, NormValues);
            }

            IReadOnlyCollection<NamedOnnxValue> inputs = new NamedOnnxValue[]
            {
                NamedOnnxValue.CreateFromTensor(_inputName, inputTensors)
            };

            try
            {
                using (IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = _crnnNet.Run(inputs))
                {
                    var result = results[0];
                    var dimensions = result.AsTensor<float>().Dimensions;
                    ReadOnlySpan<float> outputData = result.AsEnumerable<float>().ToArray();

                    var tl = ScoreToTextLine(outputData, dimensions[1], dimensions[2]);
                    tl.Time = sw.ElapsedMilliseconds;
                    return tl;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message + ex.StackTrace);
                //throw ex;
            }

            return new TextLine() { Chars = Array.Empty<string>(), CharScores = Array.Empty<float>(), Time = sw.ElapsedMilliseconds };
        }

        private static bool IsChinese(string c)
        {
            if (string.IsNullOrEmpty(c))
            {
                return false;
            }
            var ch = c[0];
            return ch >= '\u4e00' && ch <= '\u9fff';
        }

        private TextLine ScoreToTextLine(ReadOnlySpan<float> srcData, int h, int w)
        {
            var textIndex = new int[h];
            var textProb = new float[h];
            for (int i = 0; i < h; i++)
            {
                int maxIndex = 0;
                float maxValue = float.NegativeInfinity;
                for (int j = 0; j < w; j++)
                {
                    int idx = i * w + j;
                    if (srcData[idx] > maxValue)
                    {
                        maxIndex = j;
                        maxValue = srcData[idx];
                    }
                }
                textIndex[i] = maxIndex;
                textProb[i] = maxValue;
            }

            var selection = new bool[h];
            selection[0] = true;
            for (int i = 1; i < h; i++)
            {
                selection[i] = textIndex[i] != textIndex[i - 1];
            }
            for (int i = 0; i < h; i++)
            {
                selection[i] &= textIndex[i] != 0;
            }

            var validCols = new List<int>();
            var chars = new List<string>();
            var scores = new List<float>();
            for (int i = 0; i < h; i++)
            {
                if (selection[i])
                {
                    validCols.Add(i);
                    chars.Add(_keys[textIndex[i]]);
                    scores.Add(textProb[i]);
                }
            }

            var colWidth = new int[validCols.Count];
            if (validCols.Count > 0)
            {
                colWidth[0] = Math.Min(IsChinese(chars[0]) ? 3 : 2, validCols[0]);
                for (int k = 1; k < validCols.Count; k++)
                {
                    colWidth[k] = validCols[k] - validCols[k - 1];
                }
            }

            var finalChars = new List<string>();
            var finalScores = new List<float>();
            string? state = null;
            var wordChars = new List<string>();
            var wordScores = new List<float>();
            for (int k = 0; k < chars.Count; k++)
            {
                var currentState = IsChinese(chars[k]) ? "cn" : "en&num";
                if (state == null)
                {
                    state = currentState;
                }

                if (state != currentState || (k > 0 && colWidth[k] > 4))
                {
                    if (wordChars.Count > 0)
                    {
                        finalChars.AddRange(wordChars);
                        finalScores.AddRange(wordScores);
                        finalChars.Add(" ");
                        finalScores.Add(1.0f);
                        wordChars.Clear();
                        wordScores.Clear();
                    }
                    state = currentState;
                }

                wordChars.Add(chars[k]);
                wordScores.Add(scores[k]);
            }

            if (wordChars.Count > 0)
            {
                finalChars.AddRange(wordChars);
                finalScores.AddRange(wordScores);
            }

            return new TextLine
            {
                Chars = finalChars.ToArray(),
                CharScores = finalScores.ToArray()
            };
        }

        public void Dispose()
        {
            _crnnNet?.Dispose();
        }
    }
}