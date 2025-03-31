using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FitnessCenterIS.View.Windows
{
    public partial class TaskWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        public event EventHandler TaskCreated;
        public event EventHandler TaskUpdated;
        public ObservableCollection<TaskPriorities> Priorities { get; set; }
        public ObservableCollection<TaskStatuses> Statuses { get; set; }
        private ObservableCollection<ClientInfo> _allClients;
        private ClientInfo _selectedClient;
        private Tasks _taskToEdit;
        private bool _isEditMode = false;
        private int _taskIdToEdit; // Добавляем поле для хранения ID редактируемой задачи

        // Конструктор для создания новой задачи
        public TaskWindow()
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadPriorities();
            LoadStatuses();
            LoadClients();
            DataContext = this;
            _isEditMode = false;
            WindowTitleTextBlock.Text = "Создание новой задачи";
            AddButton.Visibility = Visibility.Visible;
            SaveButton.Visibility = Visibility.Collapsed;
        }

        // Новый конструктор для редактирования существующей задачи по ID
        public TaskWindow(int taskId)
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadPriorities();
            LoadStatuses();
            LoadClients();
            DataContext = this;
            _taskIdToEdit = taskId;
            _isEditMode = true;
            WindowTitleTextBlock.Text = "Редактирование задачи";
            AddButton.Visibility = Visibility.Collapsed;
            SaveButton.Visibility = Visibility.Visible;
            LoadTaskForEdit(); // Загружаем задачу по ID
        }

        public void SetClient(int clientId)
        {
            // Находим клиента по ID
            var clientInfo = _allClients.FirstOrDefault(c => c.ClientID == clientId);
            if (clientInfo != null)
            {
                _selectedClient = clientInfo;
                ClientTextBox.Text = clientInfo.ToString();

                // Можно также заблокировать поле выбора клиента, 
                // чтобы пользователь не мог его изменить
                ClientTextBox.IsEnabled = false;
            }
        }


        private void LoadTaskForEdit()
        {
            try
            {
                _taskToEdit = _dbContext.Tasks
                    .Include(t => t.Clients.Persons)
                    .FirstOrDefault(t => t.TaskID == _taskIdToEdit);
                if (_taskToEdit != null)
                {
                    PopulateFields();
                }
                else
                {
                    MessageBox.Show("Задача не найдена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке задачи для редактирования: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void PopulateFields()
        {
            TaskNameTextBox.Text = _taskToEdit.Name;
            TaskDescriptionTextBox.Text = _taskToEdit.Description;
            DeadlineDatePicker.SelectedDate = _taskToEdit.EndDedlainDateTime?.Date;
            DeadlineTimeTextBox.Text = _taskToEdit.EndDedlainDateTime?.ToString("HH:mm");

            PriorityComboBox.SelectedItem = Priorities.FirstOrDefault(p => p.TaskPrioritieID == _taskToEdit.TaskPrioritieID);
            StatusComboBox.SelectedItem = Statuses.FirstOrDefault(s => s.TaskStatusID == _taskToEdit.TaskStatusID);

            var clientInfo = _allClients.FirstOrDefault(c => c.ClientID == _taskToEdit.ClientID);
            if (clientInfo != null)
            {
                _selectedClient = clientInfo;
                ClientTextBox.Text = clientInfo.ToString();
            }
        }

        private void LoadPriorities()
        {
            var priorities = _dbContext.TaskPriorities.OrderBy(p => p.Name).ToList();
            Priorities = new ObservableCollection<TaskPriorities>(priorities);
            PriorityComboBox.ItemsSource = Priorities;
        }

        private void LoadStatuses()
        {
            var statuses = _dbContext.TaskStatuses.OrderBy(s => s.Name).ToList();
            Statuses = new ObservableCollection<TaskStatuses>(statuses);
            StatusComboBox.ItemsSource = Statuses;
        }

        private void LoadClients()
        {
            var clients = _dbContext.Clients
                .Select(c => new ClientInfo
                {
                    ClientID = c.ClientID,
                    FullName = c.Persons.Name + " " + c.Persons.Surname,
                    CardNumber = c.NumberCard,
                })
                .OrderBy(c => c.FullName)
                .ToList();
            _allClients = new ObservableCollection<ClientInfo>(clients);
        }

        public class ClientInfo : INotifyPropertyChanged
        {
            public int ClientID { get; set; }
            public string FullName { get; set; }
            public string CardNumber { get; set; }

            public override string ToString()
            {
                return $"{FullName} (Карта №{CardNumber})";
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void ClientTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = ClientTextBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                ClientsPopup.IsOpen = false;
                return;
            }

            var filteredClients = _allClients.Where(c =>
                c.FullName.ToLower().Contains(searchText) ||
                c.CardNumber.ToLower().Contains(searchText)).ToList();

            ClientsListBoxInPopup.ItemsSource = new ObservableCollection<ClientInfo>(filteredClients);
            if (filteredClients.Any())
            {
                ClientsPopup.IsOpen = true;
            }
            else
            {
                ClientsPopup.IsOpen = false;
            }
        }

        private void ClientsListBoxInPopup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClientsListBoxInPopup.SelectedItem is ClientInfo selectedClient)
            {
                _selectedClient = selectedClient;
                ClientTextBox.Text = selectedClient.ToString();
                ClientsPopup.IsOpen = false;
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isEditMode && _selectedClient != null)
            {
                string taskName = TaskNameTextBox.Text;
                string description = TaskDescriptionTextBox.Text;
                DateTime? deadlineDate = DeadlineDatePicker.SelectedDate;
                TimeSpan? deadlineTime = null;

                if (!string.IsNullOrWhiteSpace(DeadlineTimeTextBox.Text))
                {
                    TimeSpan parsedTime;
                    if (TimeSpan.TryParse(DeadlineTimeTextBox.Text, out parsedTime))
                    {
                        deadlineTime = parsedTime;
                    }
                    else
                    {
                        MessageBox.Show("Некорректный формат времени.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                DateTime? endDeadlineDateTime = null;
                if (deadlineDate.HasValue)
                {
                    endDeadlineDateTime = deadlineDate.Value.Date + (deadlineTime ?? TimeSpan.Zero);
                }

                if (string.IsNullOrWhiteSpace(taskName))
                {
                    MessageBox.Show("Пожалуйста, введите название задачи.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (PriorityComboBox.SelectedItem is TaskPriorities selectedPriority)
                {
                    if (StatusComboBox.SelectedItem is TaskStatuses selectedStatus)
                    {
                        var newTask = new Tasks
                        {
                            Name = taskName,
                            Description = description,
                            EndDedlainDateTime = endDeadlineDateTime,
                            TaskPrioritieID = selectedPriority.TaskPrioritieID,
                            TaskStatusID = selectedStatus.TaskStatusID,
                            ClientID = _selectedClient.ClientID
                        };

                        try
                        {
                            _dbContext.Tasks.Add(newTask);
                            _dbContext.SaveChanges();
                            TaskCreated?.Invoke(this, EventArgs.Empty);
                            this.Close();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Ошибка при добавлении задачи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            // Рассмотрите возможность логирования более подробной информации об ошибке
                        }
                    }
                    else
                    {
                        MessageBox.Show("Пожалуйста, выберите статус задачи.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Пожалуйста, выберите приоритет задачи.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (!_isEditMode && _selectedClient == null)
            {
                MessageBox.Show("Пожалуйста, выберите клиента.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditMode && _taskToEdit != null)
            {
                _taskToEdit.Name = TaskNameTextBox.Text;
                _taskToEdit.Description = TaskDescriptionTextBox.Text;
                _taskToEdit.EndDedlainDateTime = DeadlineDatePicker.SelectedDate?.Date + (TimeSpan.TryParse(DeadlineTimeTextBox.Text, out var time) ? time : TimeSpan.Zero);

                if (PriorityComboBox.SelectedItem is TaskPriorities selectedPriority)
                {
                    _taskToEdit.TaskPrioritieID = selectedPriority.TaskPrioritieID;
                }
                if (StatusComboBox.SelectedItem is TaskStatuses selectedStatus)
                {
                    _taskToEdit.TaskStatusID = selectedStatus.TaskStatusID;
                }
                if (_selectedClient != null)
                {
                    _taskToEdit.ClientID = _selectedClient.ClientID;
                }

                try
                {
                    _dbContext.Entry(_taskToEdit).State = EntityState.Modified;
                    await _dbContext.SaveChangesAsync();

                    TaskUpdated?.Invoke(this, EventArgs.Empty);
                    this.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении изменений: {ex.Message}\n\n{ex.InnerException?.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}