namespace SoftwareEngineering.BLL.Models
{
    public enum TaskSortOption
    {
        ByTitleAsc,       // За назвою (А-Я)
        ByPriorityDesc,   // За пріоритетом (High -> Low)
        ByDeadlineAsc,    // За дедлайном (Найближчі спочатку)
        ByStatus          // За статусом виконання (групи від «До виконання» до «Виконано»)
    }
}