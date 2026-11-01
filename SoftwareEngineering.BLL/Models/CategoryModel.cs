namespace SoftwareEngineering.BLL.Models;

public class CategoryModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ColorHex { get; set; } = string.Empty;

    public int TasksCount { get; set; }
}

public class CreateCategoryModel
{
    public string Name { get; set; } = string.Empty;

    public string ColorHex { get; set; } = string.Empty;
}

public class UpdateCategoryModel
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ColorHex { get; set; } = string.Empty;
}