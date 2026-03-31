using Microsoft.ML.Data;

namespace AgriPredict.Training;

/// <summary>
/// ML.NET input row for the FrostRisk binary classifier.
/// Property names become IDataView column names.
/// The label column must be named "Label" (ML.NET convention for binary classification).
/// </summary>
internal sealed class FrostRiskInput
{
    public float TempMin { get; set; }
    public float TempMax { get; set; }
    public float Precipitation { get; set; }
    public float WindSpeed { get; set; }

    /// <summary>Day-of-year (1–366) stored as float for ML.NET feature concatenation.</summary>
    public float DayOfYear { get; set; }

    [ColumnName("Label")]
    public bool FrostRisk { get; set; }
}
