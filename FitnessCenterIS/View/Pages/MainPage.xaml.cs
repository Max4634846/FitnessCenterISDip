using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FitnessCenterIS.View.Windows;
using System.Data.Entity;

namespace FitnessCenterIS.View.Pages
{
    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class MainPage : Page
    {
        private readonly BDFitnessClubDipEntities _dbContext = new BDFitnessClubDipEntities();
        private ObservableCollection<object> _allTasks;

        public int NewTasksCount { get; set; }
        public int InProgressTasksCount { get; set; }
        public int CompletedTasksCount { get; set; }

        public MainPage()
        {
            InitializeComponent();
            LoadTasksForClient();
        }

        private void LoadTasksForClient()
        {
            try
            {
                var tasks = _dbContext.Tasks
                    .Select(t => new
                    {
                        t.Name,
                        t.Description,
                        t.EndDedlainDateTime,
                        TaskPrioritie = t.TaskPriorities.Name,
                        TaskStatus = t.TaskStatuses.Name,
                        Client = t.Clients.Persons.Surname + " " + t.Clients.Persons.Name + " " +  t.Clients.Persons.MiddleName,
                        t.TaskStatusID,
                        t.TaskID
                    })
                    .ToList();

                _allTasks = new ObservableCollection<object>(tasks);
                UpdateTaskCounts(_allTasks);

                var initialTasks = _allTasks.Where(task => (task as dynamic)?.TaskStatusID == 1).ToList();
                TasksListBox.ItemsSource = new ObservableCollection<object>(initialTasks);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке задач: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                // Рассмотрите возможность логирования более подробной информации об ошибке
            }
        }

        private void UpdateTaskCounts(ObservableCollection<object> tasks)
        {
            NewTasksCount = tasks.Count(task => (task as dynamic)?.TaskStatusID == 1);
            InProgressTasksCount = tasks.Count(task => (task as dynamic)?.TaskStatusID == 2);
            CompletedTasksCount = tasks.Count(task => (task as dynamic)?.TaskStatusID == 3);
            UpdateButtonsContent();
        }

        private void UpdateButtonsContent()
        {
            NewTasksButton.Content = $"Новый ({NewTasksCount})";
            InProgressTasksButton.Content = $"В работе ({InProgressTasksCount})";
            CompletedTasksButton.Content = $"Завершенные ({CompletedTasksCount})";
        }

        private void AddNewClientButton_Click(object sender, RoutedEventArgs e)
        {
            AddEditNewClientWindow addEditNewClientWindow = new AddEditNewClientWindow(isLead: false);
            addEditNewClientWindow.ShowDialog();
        }

        private void OpenWinTask_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var taskWindow = new TaskWindow();
            taskWindow.TaskCreated += TaskWindow_TaskCreatedFromAdd;
            taskWindow.ShowDialog();
        }

        private void TaskWindow_TaskCreatedFromAdd(object sender, EventArgs e)
        {
            LoadTasksForClient();
        }

        private void FilterTasksByStatus_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                if (clickedButton.Tag is string tagString && int.TryParse(tagString, out int statusId))
                {
                    var filteredTasks = _allTasks.Where(task => (task as dynamic)?.TaskStatusID == statusId).ToList();
                    TasksListBox.ItemsSource = new ObservableCollection<object>(filteredTasks);
                }
            }
        }

        private void TasksListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TasksListBox.SelectedItem is object selectedTask)
            {
                dynamic task = selectedTask;
                int taskId = task.TaskID;

                try
                {
                    // Передаем только ID задачи
                    var taskWindow = new TaskWindow(taskId);
                    taskWindow.TaskUpdated += TaskWindow_TaskUpdatedFromEdit;
                    taskWindow.ShowDialog();
                    TasksListBox.SelectedItem = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при открытии задачи на редактирование: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TaskWindow_TaskUpdatedFromEdit(object sender, EventArgs e)
        {
            LoadTasksForClient();
        }
        private void VisitBtn_Click(object sender, RoutedEventArgs e)
        {
            ObservableCollection<Clients> clientList = GetClientList();
            Window parentWindow = Window.GetWindow(this);
            if (parentWindow is MenuWindow menuWindow)
            {
                VisitClientWindow visitClientWin = new VisitClientWindow(clientList, menuWindow);
                visitClientWin.Show();
            }
            else
            {
                MessageBox.Show("Не удалось найти главное окно MenuWindow.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private ObservableCollection<Clients> GetClientList()
        {
            try
            {
                // Используйте ваш существующий контекст базы данных _dbContext
                // Ensure you load the Persons navigation property here
                return new ObservableCollection<Clients>(_dbContext.Clients.Include(c => c.Persons).ToList());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке списка клиентов: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return new ObservableCollection<Clients>(); // Верните пустой список в случае ошибки
            }
        }

        private void NewSaleBtn_Click(object sender, RoutedEventArgs e)
        {
            int client = 0;
            NewSaleWindow newSaleWindow = new NewSaleWindow(client);
            newSaleWindow.ShowDialog();
        }

        private void NewLeadButton_Click(object sender, RoutedEventArgs e)
        {
            AddEditNewClientWindow addEditNewClientWindow = new AddEditNewClientWindow(isLead: true);
            addEditNewClientWindow.ShowDialog();
        }

        private void AttendanceBtn_Click(object sender, RoutedEventArgs e)
        {
            AttendanceWindow attendanceWindow = new AttendanceWindow();
            attendanceWindow.ShowDialog();
        }
    }
}