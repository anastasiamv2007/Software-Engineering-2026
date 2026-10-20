using SoftwareEngineering.BLL.Interfaces;
using System.Windows;

namespace SoftwareEngineering.Client
{
    public partial class MainWindow : Window
    {
        private readonly ITaskService taskService;
        private readonly IProjectService projectService;

        // Конструктор приймає сервіс через ін'єкцію залежностей (Dependency Injection)
        public MainWindow(ITaskService taskService, IProjectService projectService)
        {
            InitializeComponent();
            this.taskService = taskService ?? throw new ArgumentNullException(nameof(taskService));
            this.projectService = projectService ?? throw new ArgumentNullException(nameof(projectService));

            // Завантажуємо список завдань при запуску головного вікна
            LoadTasks();
        }

        private void OpenCreateTaskWindow_Click(object sender, RoutedEventArgs e)
        {
            // Передаємо сервіс у вікно створення завдання
            var createWindow = new CreateTaskView(taskService, projectService)
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

        // Обробник для Учасника 2 (Перегляд деталей)
        private void ViewDetailsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Отримуємо вибране завдання з DataGrid
            if (TasksDataGrid.SelectedItem is SoftwareEngineering.BLL.Models.TaskModel selectedTask)
            {
                // Припускаємо, що вікно TaskDetailsWindow приймає TaskModel у конструкторі
                var detailsWindow = new TaskDetailsWindow(taskService, selectedTask.Id)
                {
                    Owner = this
                };
                detailsWindow.ShowDialog();
            }
            else
            {
                MessageBox.Show("Будь ласка, виберіть завдання для перегляду.", "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        //3
        private void EditTaskMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // Отримуємо вибране завдання з DataGrid
            if (TasksDataGrid.SelectedItem is SoftwareEngineering.BLL.Models.TaskModel selectedTask)
            {
                // Відкриваємо твоє вікно редагування і передаємо туди сервіс та вибране завдання
                var editWindow = new EditTaskWindow(taskService, selectedTask)
                {
                    Owner = this
                };

                // Якщо редагування пройшло успішно (користувач натиснув "Зберегти"), оновлюємо список
                if (editWindow.ShowDialog() == true)
                {
                    LoadTasks();
                }
            }
            else
            {
                MessageBox.Show("Будь ласка, виберіть завдання для редагування.", "Увага", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void DeleteTaskMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var deleteWindow = new DeleteTaskWindow(taskService)
            {
                Owner = this
            };

            // Відкриваємо діалог
            if (deleteWindow.ShowDialog() == true)
            {
                LoadTasks(); // якщо щось видалили — оновити таблицю
            }
        }

        // 7
        private void UpdateProgressMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var progressWindow = new UpdateProgressWindow(taskService)
            {
                Owner = this
            };

            if (progressWindow.ShowDialog() == true)
            {
                LoadTasks();
            }
        }


    }

}