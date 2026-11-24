namespace SoftwareEngineering.BLL.Models;

public class TaskStatisticsModel
{
    public int Total { get; set; }

    public int Completed { get; set; }

    public int InProgress { get; set; }

    public int Active { get; set; }

    public int Overdue { get; set; }

    public double CompletedPercent => this.Total == 0 ? 0 : this.Completed * 100.0 / this.Total;
}