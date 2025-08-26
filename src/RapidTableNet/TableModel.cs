namespace RapidTableNet;

public enum TableModel
{
    SlanetPlus,
    PpStructureMobileV2
}

public static class TableModelExtensions
{
    public static string GetFileName(this TableModel model) => model switch
    {
        TableModel.SlanetPlus => "slanet-plus.onnx",
        TableModel.PpStructureMobileV2 => "en_ppstructure_mobile_v2.0_SLANet_infer.onnx",
        _ => throw new ArgumentOutOfRangeException(nameof(model), model, null)
    };
}
