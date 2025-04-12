using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FitnessCenterIS.View.Windows
{
    public partial class AttendanceWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private ObservableCollection<ClientInfo> _allClients;
        private ObservableCollection<StaffInfo> _allStaff;
        private ClientInfo _selectedClient;
        private StaffInfo _selectedStaff;
        private int _selectedClientId;
        private int _selectedStaffId;
        private Attendances _currentAttendance;
        private bool _isClientMode = true;

        public AttendanceWindow()
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadClients();
            LoadStaff();
            this.DataContext = this;

            // Устанавливаем начальную видимость элементов
            UpdateUIVisibility();
        }

        private void LoadClients()
        {
            var clients = _dbContext.Clients
                .Select(c => new ClientInfo
                {
                    ClientID = c.ClientID,
                    FullName = c.Persons.Surname + " " + c.Persons.Name + " " + c.Persons.MiddleName,
                    CardNumber = c.NumberCard,
                    Gender = c.Persons.Gender,
                    Status = c.StatusClient
                })
                .OrderBy(c => c.FullName)
                .ToList();
            _allClients = new ObservableCollection<ClientInfo>(clients);
        }

        private void LoadStaff()
        {
            var staff = _dbContext.Staffs
                .Select(s => new StaffInfo
                {
                    StaffID = s.StaffID,
                    FullName = s.Persons.Surname + " " + s.Persons.Name + " " + s.Persons.MiddleName,
                    Role = s.Roles.Name,
                    Gender = s.Persons.Gender
                })
                .OrderBy(s => s.FullName)
                .ToList();
            _allStaff = new ObservableCollection<StaffInfo>(staff);
        }

        private void UserType_Changed(object sender, RoutedEventArgs e)
        {
            _isClientMode = ClientRadioButton.IsChecked ?? true;

            // Сбрасываем выбранные значения
            _selectedClient = null;
            _selectedStaff = null;
            _selectedClientId = 0;
            _selectedStaffId = 0;

            if (ClientTextBox != null)
            {
                ClientTextBox.Text = string.Empty;
            }

            // Обновляем видимость элементов UI
            UpdateUIVisibility();
        }


        private void UpdateUIVisibility()
        {
            bool showClientElements = _isClientMode;
            bool isLead = _selectedClient != null && _selectedClient.Status == "Лид";

            // Элементы для абонементов скрыты для "Лид" и сотрудников
            bool showSeasonTickets = _isClientMode && !isLead;

            // Элементы для услуг показываем только для клиентов
            bool showServices = _isClientMode;

            // Заголовок поиска
            var searchLabel = (_isClientMode) ? "Поиск клиента" : "Поиск сотрудника";
            var textBlocks = FindVisualChildren<TextBlock>(this);
            var searchTextBlock = textBlocks.FirstOrDefault(t => t.Text == "Поиск клиента" || t.Text == "Поиск сотрудника");
            if (searchTextBlock != null)
                searchTextBlock.Text = searchLabel;

            // Полное скрытие секции абонементов
            var seasonTicketsLabel = FindName("SeasonTicketsLabel") as TextBlock;
            if (seasonTicketsLabel != null)
                seasonTicketsLabel.Visibility = showSeasonTickets ? Visibility.Visible : Visibility.Collapsed;

            var seasonTicketsBorder = FindName("SeasonTicketsBorder") as Border;
            if (seasonTicketsBorder != null)
                seasonTicketsBorder.Visibility = showSeasonTickets ? Visibility.Visible : Visibility.Collapsed;

            // Полное скрытие секции услуг
            var servicesLabel = FindName("ServicesLabel") as TextBlock;
            if (servicesLabel != null)
            {
                servicesLabel.Visibility = showServices ? Visibility.Visible : Visibility.Collapsed;
                if (isLead && showServices)
                    servicesLabel.Text = "Доступные услуги";
                else if (showServices)
                    servicesLabel.Text = "Услуги абонемента";
            }

            var servicesBorder = FindName("ServicesBorder") as Border;
            if (servicesBorder != null)
                servicesBorder.Visibility = showServices ? Visibility.Visible : Visibility.Collapsed;
        }


        private IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
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

            if (_isClientMode)
            {
                var filteredClients = _allClients.Where(c =>
                    c.FullName.ToLower().Contains(searchText) ||
                    (c.CardNumber != null && c.CardNumber.ToLower().Contains(searchText))).ToList();

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
            else
            {
                var filteredStaff = _allStaff.Where(s =>
                    s.FullName.ToLower().Contains(searchText) ||
                    s.Role.ToLower().Contains(searchText)).ToList();

                ClientsListBoxInPopup.ItemsSource = new ObservableCollection<StaffInfo>(filteredStaff);
                if (filteredStaff.Any())
                {
                    ClientsPopup.IsOpen = true;
                }
                else
                {
                    ClientsPopup.IsOpen = false;
                }
            }
        }

        private void ClientsListBoxInPopup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isClientMode)
            {
                if (ClientsListBoxInPopup.SelectedItem is ClientInfo selectedClient)
                {
                    _selectedClient = selectedClient;
                    _selectedClientId = selectedClient.ClientID;
                    ClientTextBox.Text = selectedClient.ToString();
                    ClientsPopup.IsOpen = false;

                    // Проверяем, есть ли у клиента активное посещение
                    CheckClientActiveAttendance(_selectedClientId);

                    bool isLead = selectedClient.Status == "Лид";

                    if (!isLead)
                    {
                        // Загружаем абонементы для обычных клиентов
                        LoadClientSeasonTickets(_selectedClientId);
                    }
                    else
                    {
                        // Для Лид-клиентов загружаем все доступные услуги
                        LoadAllServices();
                    }

                    // Обновляем UI на основе статуса клиента
                    UpdateUIVisibility();
                }
            }
            else
            {
                if (ClientsListBoxInPopup.SelectedItem is StaffInfo selectedStaff)
                {
                    _selectedStaff = selectedStaff;
                    _selectedStaffId = selectedStaff.StaffID;
                    ClientTextBox.Text = selectedStaff.ToString();
                    ClientsPopup.IsOpen = false;

                    // Проверяем, есть ли у сотрудника активное посещение
                    CheckStaffActiveAttendance(_selectedStaffId);

                    // Обновляем UI для сотрудника
                    UpdateUIVisibility();
                }
            }
        }

        private void LoadAllServices()
        {
            // Загружаем все доступные услуги для клиентов со статусом "Лид"
            var services = _dbContext.Services
                .Where(s => s.StatusService == "Активен" && s.TrialService == true)
                .Select(s => new {
                    s.ServiceID,
                    s.Name,
                    s.Description,
                    s.Price,
                    RemainingVisits = 1 // Одно пробное посещение
                })
                .ToList();

            ServicesListBox.ItemsSource = services;
            ServicesListBox.SelectedValuePath = "ServiceID";
        }

        private void CheckStaffActiveAttendance(int staffId)
        {
            // Проверяем, есть ли у сотрудника активное посещение
            _currentAttendance = _dbContext.Attendances
                .FirstOrDefault(a => a.StaffID == staffId && a.ExitDateTime == null);

            if (_currentAttendance != null)
            {
                // Сотрудник уже в зале
                if (_currentAttendance.LockerID.HasValue)
                {
                    var locker = _dbContext.Lockers.FirstOrDefault(l => l.LockerID == _currentAttendance.LockerID.Value);
                    if (locker != null)
                    {
                        LockerInfoTextBlock.Text = $"Сотрудник уже на территории. Шкафчик №{locker.KeyNumber}";
                    }
                    else
                    {
                        LockerInfoTextBlock.Text = "Сотрудник уже на территории";
                    }
                }
                else
                {
                    LockerInfoTextBlock.Text = "Сотрудник уже на территории";
                }

                LockerInfoTextBlock.Visibility = Visibility.Visible;

                // Скрываем кнопку отметки посещения и показываем кнопку завершения
                MarkAttendanceButton.Visibility = Visibility.Collapsed;
                CompleteAttendanceButton.Visibility = Visibility.Visible;
            }
            else
            {
                // Сотрудник не в зале
                LockerInfoTextBlock.Visibility = Visibility.Collapsed;

                // Показываем кнопку отметки посещения и скрываем кнопку завершения
                MarkAttendanceButton.Visibility = Visibility.Visible;
                CompleteAttendanceButton.Visibility = Visibility.Collapsed;
            }
        }


        private void LoadClientSeasonTickets(int clientId)
        {
            // Загружаем абонементы клиента через SeasonticketClients
            var clientSeasonTickets = _dbContext.SeasonticketClients
                .Where(stc => stc.ClientID == clientId)
                .Join(_dbContext.Seasontickets,
                    stc => stc.SeasonticketID,
                    st => st.SeasonticketID,
                    (stc, st) => new {
                        stc.SeasonticketClientID,
                        st.SeasonticketID,
                        st.Name,
                        st.Description,
                        st.ValidityDuration,
                        Status = st.StatusSeasonticket
                    })
                .Where(st => st.Status == "Активен")
                .ToList();

            // Очищаем коллекцию перед установкой ItemsSource
            //SeasonTicketsListBox.Items.Clear();
            SeasonTicketsListBox.ItemsSource = clientSeasonTickets;
            SeasonTicketsListBox.SelectedValuePath = "SeasonticketID";
        }


        private void LoadSeasonTicketServices(int seasonTicketId)
        {
            var services = _dbContext.SeasonticketServices
                .Where(sts => sts.SeasonticketID == seasonTicketId)
                .Join(_dbContext.Services,
                    sts => sts.ServiceID,
                    s => s.ServiceID,
                    (sts, s) => new {
                        s.ServiceID,
                        s.Name,
                        RemainingVisits = sts.VisitLimit
                    })
                .ToList();

            ServicesListBox.ItemsSource = services;
            ServicesListBox.SelectedValuePath = "ServiceID";
        }

        private void SeasonTicketsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SeasonTicketsListBox.SelectedValue is int seasonTicketId)
            {
                LoadSeasonTicketServices(seasonTicketId);
            }
        }

        private void CheckClientActiveAttendance(int clientId)
        {
            // Проверяем, есть ли у клиента активное посещение
            _currentAttendance = _dbContext.Attendances
                .FirstOrDefault(a => a.ClientID == clientId && a.ExitDateTime == null);

            if (_currentAttendance != null)
            {
                // Клиент уже в зале, показываем информацию о шкафчике
                var locker = _dbContext.Lockers.FirstOrDefault(l => l.LockerID == _currentAttendance.LockerID);
                if (locker != null)
                {
                    LockerInfoTextBlock.Text = $"Клиент уже в зале. Шкафчик №{locker.KeyNumber}";
                    LockerInfoTextBlock.Visibility = Visibility.Visible;
                }

                // Скрываем кнопку отметки посещения и показываем кнопку завершения
                MarkAttendanceButton.Visibility = Visibility.Collapsed;
                CompleteAttendanceButton.Visibility = Visibility.Visible;

                // Отключаем выбор абонемента
                SeasonTicketsListBox.IsEnabled = false;
            }
            else
            {
                // Клиент не в зале, скрываем информацию о шкафчике
                LockerInfoTextBlock.Visibility = Visibility.Collapsed;

                // Показываем кнопку отметки посещения и скрываем кнопку завершения
                MarkAttendanceButton.Visibility = Visibility.Visible;
                CompleteAttendanceButton.Visibility = Visibility.Collapsed;

                // Включаем выбор абонемента
                SeasonTicketsListBox.IsEnabled = true;
            }
        }

        private void MarkAttendance_Click(object sender, RoutedEventArgs e)
        {
            if (_isClientMode)
            {
                MarkClientAttendance();
            }
            else
            {
                MarkStaffAttendance();
            }
        }

        private void MarkClientAttendance()
        {
            if (_selectedClientId <= 0)
            {
                MessageBox.Show("Пожалуйста, выберите клиента.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool isLead = _selectedClient.Status == "Лид";

            if (!isLead && (SeasonTicketsListBox.SelectedValue == null))
            {
                MessageBox.Show("Пожалуйста, выберите абонемент.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ServicesListBox.SelectedValue == null)
            {
                MessageBox.Show("Пожалуйста, выберите услугу.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int serviceId = (int)ServicesListBox.SelectedValue;
            int? seasonTicketId = isLead ? null : (int?)SeasonTicketsListBox.SelectedValue;

            // Открываем окно выбора шкафчика
            var lockerWindow = new LockerSelectionWindow(_dbContext, _selectedClient.Gender == "Мужской");
            if (lockerWindow.ShowDialog() == true)
            {
                int lockerId = lockerWindow.SelectedLockerId;

                // После выбора шкафчика создаем запись о посещении
                var attendance = new Attendances
                {
                    ClientID = _selectedClientId,
                    EntryDateTime = DateTime.Now,
                    Note = isLead ? "Пробное посещение" : "Посещение отмечено через систему",
                    LockerID = lockerId
                };

                // Занимаем шкафчик
                var locker = _dbContext.Lockers.FirstOrDefault(l => l.LockerID == lockerId);
                if (locker != null)
                {
                    locker.IsAvailable = false;
                }

                _dbContext.Attendances.Add(attendance);

                if (!isLead && seasonTicketId.HasValue)
                {
                    // Уменьшаем количество доступных посещений для конкретной услуги
                    var serviceInSeasonTicket = _dbContext.SeasonticketServices
                        .FirstOrDefault(sts => sts.SeasonticketID == seasonTicketId.Value && sts.ServiceID == serviceId);

                    if (serviceInSeasonTicket != null && serviceInSeasonTicket.VisitLimit > 0)
                    {
                        serviceInSeasonTicket.VisitLimit--;
                    }
                }

                _dbContext.SaveChanges();

                MessageBox.Show("Посещение успешно отмечено.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);

                // Обновляем интерфейс
                CheckClientActiveAttendance(_selectedClientId);
            }
        }

        private void MarkStaffAttendance()
        {
            if (_selectedStaffId <= 0)
            {
                MessageBox.Show("Пожалуйста, выберите сотрудника.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Открываем окно выбора шкафчика для сотрудника
            var lockerWindow = new LockerSelectionWindow(_dbContext, _selectedStaff.Gender == "Мужской");
            if (lockerWindow.ShowDialog() == true)
            {
                int lockerId = lockerWindow.SelectedLockerId;

                // Создаем запись о посещении с указанием шкафчика
                var attendance = new Attendances
                {
                    StaffID = _selectedStaffId,
                    EntryDateTime = DateTime.Now,
                    Note = "Отметка сотрудника",
                    LockerID = lockerId // Добавляем шкафчик
                };

                // Занимаем шкафчик
                var locker = _dbContext.Lockers.FirstOrDefault(l => l.LockerID == lockerId);
                if (locker != null)
                {
                    locker.IsAvailable = false;
                }

                _dbContext.Attendances.Add(attendance);
                _dbContext.SaveChanges();

                MessageBox.Show("Посещение сотрудника успешно отмечено.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);

                // Обновляем интерфейс
                CheckStaffActiveAttendance(_selectedStaffId);
            }
        }


        private void CompleteAttendance_Click(object sender, RoutedEventArgs e)
        {
            if (_currentAttendance == null)
            {
                MessageBox.Show("Нет активного посещения для завершения.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Завершаем посещение
            _currentAttendance.ExitDateTime = DateTime.Now;

            // Освобождаем шкафчик для клиента или сотрудника
            if (_currentAttendance.LockerID.HasValue)
            {
                var locker = _dbContext.Lockers.FirstOrDefault(l => l.LockerID == _currentAttendance.LockerID.Value);
                if (locker != null)
                {
                    locker.IsAvailable = true;
                    MessageBox.Show("Посещение успешно завершено. Шкафчик освобожден.",
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                MessageBox.Show("Посещение успешно завершено.",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            _dbContext.SaveChanges();

            // Обновляем интерфейс
            _currentAttendance = null;
            if (_isClientMode && _selectedClientId > 0)
                CheckClientActiveAttendance(_selectedClientId);
            else if (!_isClientMode && _selectedStaffId > 0)
                CheckStaffActiveAttendance(_selectedStaffId);
        }


        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        // Класс для хранения информации о клиенте
        public class ClientInfo : INotifyPropertyChanged
        {
            public int ClientID { get; set; }
            public string FullName { get; set; }
            public string CardNumber { get; set; }
            public string Gender { get; set; }
            public string Status { get; set; }

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

        // Класс для хранения информации о сотруднике
        public class StaffInfo : INotifyPropertyChanged
        {
            public int StaffID { get; set; }
            public string FullName { get; set; }
            public string Role { get; set; }
            public string Gender { get; set; }

            public override string ToString()
            {
                return $"{FullName} ({Role})";
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected virtual void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }

}