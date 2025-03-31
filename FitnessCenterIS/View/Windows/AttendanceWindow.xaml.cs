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
        private ClientInfo _selectedClient;
        private int _selectedClientId;
        private Attendances _currentAttendance;

        public AttendanceWindow()
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadClients();
            this.DataContext = this;
        }

        private void LoadClients()
        {
            var clients = _dbContext.Clients
                .Select(c => new ClientInfo
                {
                    ClientID = c.ClientID,
                    FullName = c.Persons.Surname + " " + c.Persons.Name + " " + c.Persons.MiddleName,
                    CardNumber = c.NumberCard,
                    Gender = c.Persons.Gender
                })
                .OrderBy(c => c.FullName)
                .ToList();
            _allClients = new ObservableCollection<ClientInfo>(clients);
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
                _selectedClientId = selectedClient.ClientID;
                ClientTextBox.Text = selectedClient.ToString();
                ClientsPopup.IsOpen = false;

                // Проверяем, есть ли у клиента активное посещение
                CheckClientActiveAttendance(_selectedClientId);
                // Загружаем абонементы клиента
                LoadClientSeasonTickets(_selectedClientId);
            }
        }
        private void LoadClientSeasonTickets(int clientId)
        {
            // Загружаем абонементы клиента
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

            SeasonTicketsListBox.ItemsSource = clientSeasonTickets;
            SeasonTicketsListBox.SelectedValuePath = "SeasonticketClientID";
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
                        sts.VisitLimit,
                    })
                .ToList();

            ServicesListBox.ItemsSource = services;
            ServicesListBox.SelectedValuePath = "ServiceID";
        }

        // Обработчик выбора абонемента
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
            if (_selectedClientId <= 0)
            {
                MessageBox.Show("Пожалуйста, выберите клиента.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SeasonTicketsListBox.SelectedValue == null)
            {
                MessageBox.Show("Пожалуйста, выберите абонемент.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (ServicesListBox.SelectedValue == null)
            {
                MessageBox.Show("Пожалуйста, выберите услугу.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Получаем выбранные значения
            int seasonTicketClientId = (int)SeasonTicketsListBox.SelectedValue;
            int serviceId = (int)ServicesListBox.SelectedValue;

            // Открываем окно выбора шкафчика...

            // После выбора шкафчика создаем запись о посещении
            var attendance = new Attendances
            {
                ClientID = _selectedClientId,
                EntryDateTime = DateTime.Now,
                Note = "Посещение отмечено через систему"
            };

            _dbContext.Attendances.Add(attendance);

            // Уменьшаем количество доступных посещений для конкретной услуги
            var serviceInSeasonTicket = _dbContext.SeasonticketServices
                .FirstOrDefault(sts => sts.SeasonticketID == seasonTicketClientId && sts.ServiceID == serviceId);

            if (serviceInSeasonTicket != null && serviceInSeasonTicket.VisitLimit > 0)
            {
                serviceInSeasonTicket.VisitLimit--;
            }

            _dbContext.SaveChanges();
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

            // Освобождаем шкафчик
            if (_currentAttendance.LockerID.HasValue)
            {
                var locker = _dbContext.Lockers.FirstOrDefault(l => l.LockerID == _currentAttendance.LockerID.Value);
                if (locker != null)
                {
                    locker.IsAvailable = true;
                }
            }

            _dbContext.SaveChanges();

            MessageBox.Show("Посещение успешно завершено. Шкафчик освобожден.",
                "Информация", MessageBoxButton.OK, MessageBoxImage.Information);

            // Обновляем интерфейс
            _currentAttendance = null;
            CheckClientActiveAttendance(_selectedClientId);
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
    }

}
