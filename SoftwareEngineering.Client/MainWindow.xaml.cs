using SoftwareEngineering.BLL.Interfaces;
using System.Windows;

namespace SoftwareEngineering.Client
{
    public partial class MainWindow : Window
    {
        private readonly ITaskService taskService;

        // Конструктор приймає сервіс через ін'єкцію залежностей (Dependency Injection)
        public MainWindow(ITaskService taskService)
        {
            InitializeComponent();
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));

            // Завантажуємо список завдань при запуску головного вікна
            LoadTasks();
        }

        private void OpenCreateTaskWindow_Click(object sender, RoutedEventArgs e)
        {
            // Передаємо сервіс у вікно створення завдання
            var createWindow = new CreateTaskView(taskService)
            {
                Owner = this
            };

            // Якщо завдання успішно створено, оновлюємо список
            if (createWindow.ShowDialog() == true)
            {
                LoadTasks();
            }
        }

        private void LoadTasks()
        {
            // Тут згодом буде логіка завантаження списку завдань у ваш DataGrid чи UI-елемент.
            // Поки що залишаємо метод порожнім, щоб він не викликав помилок компіляції.
        }
    }
}