namespace HorizonNova.AITrainingModule.Models;

public record EpochMetric(
    int Epoch,
    int TotalEpochs,
    double BoxLoss,
    double ClsLoss,
    double DflLoss,
    double ValBoxLoss,
    double ValClsLoss,
    double Precision,
    double Recall,
    double mAP50,
    double mAP5095,
    double GpuMemGB)
{
    public double TrainLoss => BoxLoss + ClsLoss + DflLoss;
    public double ValLoss   => ValBoxLoss + ValClsLoss;
}
