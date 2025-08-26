using Microsoft.ML.OnnxRuntime.Tensors;

namespace RapidTableNet;

internal sealed class TableLabelDecoder
{
    private readonly List<string> _character;
    private readonly Dictionary<string, int> _charToIndex;
    private readonly int _endIdx;
    private readonly HashSet<int> _ignored;
    private static readonly HashSet<string> TdTokens = new(["<td>", "<td", "<td></td>"]);

    public TableLabelDecoder(TableModel model)
    {
        var chars = TableCharacters.Get(model).ToList();
        if (!chars.Contains("<td></td>"))
            chars.Add("<td></td>");
        if (chars.Contains("<td>"))
            chars.Remove("<td>");

        chars.Insert(0, "sos");
        chars.Add("eos");

        _character = chars;
        _charToIndex = new();
        for (int i = 0; i < chars.Count; i++)
        {
            _charToIndex[chars[i]] = i;
        }

        _endIdx = _charToIndex["eos"];
        _ignored = new HashSet<int> { _charToIndex["sos"], _endIdx };
    }

    public (List<string> Structure, List<float[]>) Decode(Tensor<float> bboxPreds, Tensor<float> structProbs, float[] shape)
    {
        int seqLen = structProbs.Dimensions[1];
        int classLen = structProbs.Dimensions[2];
        var structure = new List<string>();
        var bboxes = new List<float[]>();

        for (int i = 0; i < seqLen; i++)
        {
            int bestIdx = 0;
            float bestScore = float.MinValue;
            for (int j = 0; j < classLen; j++)
            {
                float score = structProbs[0, i, j];
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIdx = j;
                }
            }

            if (i > 0 && bestIdx == _endIdx) break;
            if (_ignored.Contains(bestIdx)) continue;

            string text = _character[bestIdx];
            if (TdTokens.Contains(text))
            {
                int boxDims = bboxPreds.Dimensions[2];
                var box = new float[boxDims];
                for (int k = 0; k < boxDims; k++)
                {
                    box[k] = bboxPreds[0, i, k];
                }
                BboxDecode(box, shape);
                bboxes.Add(box);
            }
            structure.Add(text);
        }
        return (structure, bboxes);
    }

    private static void BboxDecode(float[] box, float[] shape)
    {
        float h = shape[0];
        float w = shape[1];
        for (int i = 0; i < box.Length; i += 2)
        {
            box[i] *= w;
            if (i + 1 < box.Length)
                box[i + 1] *= h;
        }
    }
}
