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
                IntraOpNumThreads = numThread
            };

            _crnnNet = new InferenceSession(path, op);
            _inputName = _crnnNet.InputMetadata.Keys.First();

            var metaMap = _crnnNet.ModelMetadata?.CustomMetadataMap;
            if (metaMap != null && metaMap.TryGetValue("character", out var chars))
            {
                var lines = chars.Split('\n');
                var keys = new List<string> { "#" };
                keys.AddRange(lines);
                keys.Add(" ");
                _keys = keys.ToArray();
            }
            else
            {
                _keys = LatinV3Keys;
            }
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

        private static readonly string[] LatinV3Keys = new[] {
    "#",
    " ",
    "!",
    "\"",
    "#",
    "$",
    "%",
    "&",
    "'",
    "(",
    ")",
    "*",
    "+",
    ",",
    "-",
    ".",
    "/",
    "0",
    "1",
    "2",
    "3",
    "4",
    "5",
    "6",
    "7",
    "8",
    "9",
    ":",
    ";",
    "<",
    "=",
    ">",
    "?",
    "@",
    "A",
    "B",
    "C",
    "D",
    "E",
    "F",
    "G",
    "H",
    "I",
    "J",
    "K",
    "L",
    "M",
    "N",
    "O",
    "P",
    "Q",
    "R",
    "S",
    "T",
    "U",
    "V",
    "W",
    "X",
    "Y",
    "Z",
    "[",
    "]",
    "_",
    "`",
    "a",
    "b",
    "c",
    "d",
    "e",
    "f",
    "g",
    "h",
    "i",
    "j",
    "k",
    "l",
    "m",
    "n",
    "o",
    "p",
    "q",
    "r",
    "s",
    "t",
    "u",
    "v",
    "w",
    "x",
    "y",
    "z",
    "{",
    "}",
    "\u00a1",
    "\u00a3",
    "\u00a7",
    "\u00aa",
    "\u00ab",
    "\u00ad",
    "\u00b0",
    "\u00b2",
    "\u00b3",
    "\u00b4",
    "\u00b5",
    "\u00b7",
    "\u00ba",
    "\u00bb",
    "\u00bf",
    "\u00c0",
    "\u00c1",
    "\u00c2",
    "\u00c4",
    "\u00c5",
    "\u00c7",
    "\u00c8",
    "\u00c9",
    "\u00ca",
    "\u00cb",
    "\u00cc",
    "\u00cd",
    "\u00ce",
    "\u00cf",
    "\u00d2",
    "\u00d3",
    "\u00d4",
    "\u00d5",
    "\u00d6",
    "\u00da",
    "\u00dc",
    "\u00dd",
    "\u00df",
    "\u00e0",
    "\u00e1",
    "\u00e2",
    "\u00e3",
    "\u00e4",
    "\u00e5",
    "\u00e6",
    "\u00e7",
    "\u00e8",
    "\u00e9",
    "\u00ea",
    "\u00eb",
    "\u00ec",
    "\u00ed",
    "\u00ee",
    "\u00ef",
    "\u00f1",
    "\u00f2",
    "\u00f3",
    "\u00f4",
    "\u00f5",
    "\u00f6",
    "\u00f8",
    "\u00f9",
    "\u00fa",
    "\u00fb",
    "\u00fc",
    "\u00fd",
    "\u0105",
    "\u0106",
    "\u0107",
    "\u010c",
    "\u010d",
    "\u0110",
    "\u0111",
    "\u0119",
    "\u0131",
    "\u0141",
    "\u0142",
    "\u014d",
    "\u0152",
    "\u0153",
    "\u0160",
    "\u0161",
    "\u0178",
    "\u017d",
    "\u017e",
    "\u0292",
    "\u03b2",
    "\u03b4",
    "\u03b5",
    "\u0437",
    "\u1e60",
    "\u2018",
    "\u20ac",
    "\u2122",
    " "
};

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

        private TextLine ScoreToTextLine(ReadOnlySpan<float> srcData, int h, int w)
        {
            int lastIndex = 0;
            var scores = new List<float>();
            var chars = new List<string>();

            for (int i = 0; i < h; i++)
            {
                int maxIndex = 0;
                float maxValue = -1000F;
                for (int j = 0; j < w; j++)
                {
                    int idx = i * w + j;
                    if (srcData[idx] > maxValue)
                    {
                        maxIndex = j;
                        maxValue = srcData[idx];
                    }
                }

                if (maxIndex > 0 && maxIndex < _keys.Length && !(i > 0 && maxIndex == lastIndex))
                {
                    scores.Add(maxValue);
                    chars.Add(_keys[maxIndex]);
                }

                lastIndex = maxIndex;
            }

            return new TextLine
            {
                Chars = chars.ToArray(),
                CharScores = scores.ToArray()
            };
        }

        public void Dispose()
        {
            _crnnNet?.Dispose();
        }
    }
}