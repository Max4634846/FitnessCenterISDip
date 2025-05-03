using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FitnessCenterIS.Model;

namespace FitnessCenterIS.View.Windows
{
    public partial class TaskWindow : Window, INotifyPropertyChanged
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        public event EventHandler TaskCreated;
        public event EventHandler TaskUpdated;
        public ObservableCollection<TaskPriorities> Priorities { get; set; }
        public ObservableCollection<TaskStatuses> Statuses { get; set; }
        public ObservableCollection<AdminInfo> Administrators { get; set; }
        private ObservableCollection<ClientInfo> _allClients;
        private ClientInfo _selectedClient;
        private Tasks _taskToEdit;
        private bool _isEditMode = false;
        private int _taskIdToEdit;
        private bool _isTaskClosed = false;

        public bool IsTaskClosed
        {
            get { return _isTaskClosed; }
            set
            {
                if (_isTaskClosed != value)
                {
                    _isTaskClosed = value;
                    OnPropertyChanged("IsTaskClosed");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Конструктор для создания новой задачи
        public TaskWindow()
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadPriorities();
            LoadStatuses();
            LoadClients();
            LoadAdministrators();
            DataContext = this;
            _isEditMode = false;
            WindowTitleTextBlock.Text = "Создание новой задачи";
            AddButton.Visibility = Visibility.Visible;
            SaveButton.Visibility = Visibility.Collapsed;

            // Установка текущей даты и времени для начала задачи
            StartDatePicker.SelectedDate = DateTime.Now.Date;
            StartTimeTextBox.Text = DateTime.Now.ToString("HH:mm");

            // Установка текущего администратора как создателя
            SetCurrentAdministratorAsCreator();
        }

        // Конструктор для редактирования существующей задачи по ID
        public TaskWindow(int taskId)
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadPriorities();
            LoadStatuses();
            LoadClients();
            LoadAdministrators();
            DataContext = this;
            _taskIdToEdit = taskId;
            _isEditMode = true;
            WindowTitleTextBlock.Text = "Редактирование задачи";
            AddButton.Visibility = Visibility.Collapsed;
            SaveButton.Visibility = Visibility.Visible;
            LoadTaskForEdit(); // Загружаем задачу по ID
        }

        private void SetCurrentAdministratorAsCreator()
        {
            if (UserSession.CurrentAdmin != null)
            {
                var currentAdminId = UserSession.CurrentAdmin.UserID;
                var currentAdmin = Administrators.FirstOrDefault(a => a.AdminID == currentAdminId);

                if (currentAdmin != null)
                {
                    CreatorAdminComboBox.SelectedItem = currentAdmin;
                }
            }
        }

        public void SetClient(int clientId)
        {
            // Находим клиента по ID
            var clientInfo = _allClients.FirstOrDefault(c => c.ClientID == clientId);
            if (clientInfo != null)
            {
                _selectedClient = clientInfo;
                ClientTextBox.Text = clientInfo.ToString();
                ClientTextBox.IsEnabled = false;
            }
        }

        private void LoadTaskForEdit()
        {
            try
            {
                _taskToEdit = _dbContext.Tasks
                    .Include(t => t.Clients.Persons)
                    .Include(t => t.Users) // Для администратора, создавшего задачу
                    .Include(t => t.Users1) // Для администратора, закрывшего задачу
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

            // Заполняем поля даты и времени начала
            StartDatePicker.SelectedDate = _taskToEdit.StartDedlainDateTime?.Date;
            StartTimeTextBox.Text = _taskToEdit.StartDedlainDateTime?.ToString("HH:mm");

            // Заполняем поля даты и времени завершения
            DeadlineDatePicker.SelectedDate = _taskToEdit.EndDedlainDateTime?.Date;
            DeadlineTimeTextBox.Text = _taskToEdit.EndDedlainDateTime?.ToString("HH:mm");

            PriorityComboBox.SelectedItem = Priorities.FirstOrDefault(p => p.TaskPrioritieID == _taskToEdit.TaskPrioritieID);

            var selectedStatus = Statuses.FirstOrDefault(s => s.TaskStatusID == _taskToEdit.TaskStatusID);
            StatusComboBox.SelectedItem = selectedStatus;

            // Проверяем, закрыта ли задача
            IsTaskClosed = selectedStatus?.Name.ToLower().Contains("завершен") ?? false;
            ClosedByAdminComboBox.IsEnabled = IsTaskClosed;

            // Заполняем поля администраторов
            CreatorAdminComboBox.SelectedItem = Administrators.FirstOrDefault(a => a.AdminID == _taskToEdit.AdministratorID);
            ClosedByAdminComboBox.SelectedItem = Administrators.FirstOrDefault(a => a.AdminID == _taskToEdit.ResponsibleAdministratorID);

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
                    CardNumber = c.Persons.NumberCard,
                })
                .OrderBy(c => c.FullName)
                .ToList();
            _allClients = new ObservableCollection<ClientInfo>(clients);
        }

        private void LoadAdministrators()
        {
            var admins = _dbContext.Users
                .Where(u => u.Staffs.Roles.Name.Contains("Администратор"))
                .Select(u => new AdminInfo
                {
                    AdminID = u.UserID,
                    FullName = u.Staffs.Persons.Name + " " + u.Staffs.Persons.Surname,
                    Login = u.Login
                })
                .OrderBy(a => a.FullName)
                .ToList();
            Administrators = new ObservableCollection<AdminInfo>(admins);

            CreatorAdminComboBox.ItemsSource = Administrators;
            ClosedByAdminComboBox.ItemsSource = Administrators;
        }

        public class AdminInfo : INotifyPropertyChanged
        {
            public int AdminID { get; set; }
            public string FullName { get; set; }
            public string Login { get; set; }

            public string DisplayName => $"{FullName} ({Login})";

            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
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

        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusComboBox.SelectedItem is TaskStatuses selectedStatus)
            {
                // Проверяем, является ли выбранный статус "Завершен" или подобным
                IsTaskClosed = selectedStatus.Name.ToLower().Contains("завершен");

                // Обновляем доступность поля выбора закрывшего администратора
                ClosedByAdminComboBox.IsEnabled = IsTaskClosed;

                // Если задача закрыта, устанавливаем текущего администратора как закрывшего
                if (IsTaskClosed)
                {
                    if (UserSession.CurrentAdmin != null)
                    {
                        var currentAdminId = UserSession.CurrentAdmin.UserID;
                        var currentAdmin = Administrators.FirstOrDefault(a => a.AdminID == currentAdminId);

                        if (currentAdmin != null)
                        {
                            ClosedByAdminComboBox.SelectedItem = currentAdmin;
                        }
                    }
                }
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

                // Получаем дату и время начала
                DateTime? startDate = StartDatePicker.SelectedDate;
                TimeSpan? startTime = null;
                if (!string.IsNullOrWhiteSpace(StartTimeTextBox.Text))
                {
                    if (TimeSpan.TryParse(StartTimeTextBox.Text, out var parsedStartTime))
                    {
                        startTime = parsedStartTime;
                    }
                    else
                    {
                        MessageBox.Show("Некорректный формат времени начала.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                // Получаем дату и время завершения
                DateTime? deadlineDate = DeadlineDatePicker.SelectedDate;
                TimeSpan? deadlineTime = null;
                if (!string.IsNullOrWhiteSpace(DeadlineTimeTextBox.Text))
                {
                    if (TimeSpan.TryParse(DeadlineTimeTextBox.Text, out var parsedTime))
                    {
                        deadlineTime = parsedTime;
                    }
                    else
                    {
                        MessageBox.Show("Некорректный формат времени завершения.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                DateTime? startDateTime = null;
                if (startDate.HasValue)
                {
                    startDateTime = startDate.Value.Date + (startTime ?? TimeSpan.Zero);
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
                        // Получаем администраторов
                        int? creatorAdminId = null;
                        int? closedByAdminId = null;

                        if (CreatorAdminComboBox.SelectedItem is AdminInfo creatorAdmin)
                        {
                            creatorAdminId = creatorAdmin.AdminID;
                        }
                        else if (UserSession.CurrentAdmin != null)
                        {
                            // Если администратор не выбран, используем текущего
                            creatorAdminId = UserSession.CurrentAdmin.UserID;
                        }

                        // Если задача закрыта, устанавливаем закрывшего администратора
                        bool isTaskClosed = selectedStatus.Name.ToLower().Contains("завершен");
                        if (isTaskClosed)
                        {
                            if (ClosedByAdminComboBox.SelectedItem is AdminInfo closedByAdmin)
                            {
                                closedByAdminId = closedByAdmin.AdminID;
                            }
                            else if (UserSession.CurrentAdmin != null)
                            {
                                // Если администратор не выбран, используем текущего
                                closedByAdminId = UserSession.CurrentAdmin.UserID;
                            }
                        }

                        var newTask = new Tasks
                        {
                            Name = taskName,
                            Description = description,
                            StartDedlainDateTime = startDateTime,
                            EndDedlainDateTime = endDeadlineDateTime,
                            TaskPrioritieID = selectedPriority.TaskPrioritieID,
                            TaskStatusID = selectedStatus.TaskStatusID,
                            ClientID = _selectedClient.ClientID,
                            AdministratorID = creatorAdminId,
                            ResponsibleAdministratorID = closedByAdminId
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

                // Обновляем дату и время начала
                _taskToEdit.StartDedlainDateTime = StartDatePicker.SelectedDate?.Date +
                    (TimeSpan.TryParse(StartTimeTextBox.Text, out var startTime) ? startTime : TimeSpan.Zero);

                // Обновляем дату и время завершения
                _taskToEdit.EndDedlainDateTime = DeadlineDatePicker.SelectedDate?.Date +
                    (TimeSpan.TryParse(DeadlineTimeTextBox.Text, out var time) ? time : TimeSpan.Zero);

                if (PriorityComboBox.SelectedItem is TaskPriorities selectedPriority)
                {
                    _taskToEdit.TaskPrioritieID = selectedPriority.TaskPrioritieID;
                }

                bool wasTaskClosedBefore = _taskToEdit.TaskStatuses.Name.ToLower().Contains("завершен");
                TaskStatuses selectedStatus = null;

                if (StatusComboBox.SelectedItem is TaskStatuses status)
                {
                    selectedStatus = status;
                    _taskToEdit.TaskStatusID = status.TaskStatusID;
                }

                // Проверяем, закрыта ли задача сейчас
                bool isTaskClosedNow = selectedStatus?.Name.ToLower().Contains("завершен") ?? false;

                // Обновляем администратора, создавшего задачу
                if (CreatorAdminComboBox.SelectedItem is AdminInfo creatorAdmin)
                {
                    _taskToEdit.AdministratorID = creatorAdmin.AdminID;
                }

                // Если задача только что была закрыта, устанавливаем текущего администратора как закрывшего
                if (!wasTaskClosedBefore && isTaskClosedNow)
                {
                    if (UserSession.CurrentAdmin != null)
                    {
                        _taskToEdit.ResponsibleAdministratorID = UserSession.CurrentAdmin.UserID;

                        // Обновляем выбранный элемент в комбобоксе для отображения
                        var currentAdmin = Administrators.FirstOrDefault(a => a.AdminID == UserSession.CurrentAdmin.UserID);
                        if (currentAdmin != null)
                        {
                            ClosedByAdminComboBox.SelectedItem = currentAdmin;
                        }
                    }
                    else if (ClosedByAdminComboBox.SelectedItem is AdminInfo closedByAdmin)
                    {
                        _taskToEdit.ResponsibleAdministratorID = closedByAdmin.AdminID;
                    }
                }
                // Если задача уже была закрыта, сохраняем выбранного администратора
                else if (isTaskClosedNow)
                {
                    if (ClosedByAdminComboBox.SelectedItem is AdminInfo closedByAdmin)
                    {
                        _taskToEdit.ResponsibleAdministratorID = closedByAdmin.AdminID;
                    }
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
