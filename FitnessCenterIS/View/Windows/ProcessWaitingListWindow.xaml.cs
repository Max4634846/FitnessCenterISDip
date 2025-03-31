using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace FitnessCenterIS.View.Windows
{
    /// <summary>
    /// Interaction logic for ProcessWaitingListWindow.xaml
    /// </summary>
    public partial class ProcessWaitingListWindow : Window
    {
        private BDFitnessClubDipEntities _dbContext;
        private int _waitingID;
        private WaitingListClients _waitingListClient;

        public ProcessWaitingListWindow(BDFitnessClubDipEntities dbContext, int waitingID)
        {
            InitializeComponent();
            _dbContext = dbContext;
            _waitingID = waitingID;
            LoadWaitingListClient();
        }

        private void LoadWaitingListClient()
        {
            _waitingListClient = _dbContext.WaitingListClients
                .Include("WaitingLists")
                .Include("WaitingLists.Services")
                .Include("Clients.Persons")
                .FirstOrDefault(wlc => wlc.WaitingID == _waitingID);

            if (_waitingListClient != null)
            {
                ClientNameTextBlock.Text = $"{_waitingListClient.Clients.Persons.Surname} {_waitingListClient.Clients.Persons.Name}";
                ServiceNameTextBlock.Text = _waitingListClient.WaitingLists.SeasonticketServices.Services.Name;
                EnrollmentDateTextBlock.Text = _waitingListClient.EnrollmentDateTime.ToString();
                NotesTextBox.Text = _waitingListClient.Notes;

                // Загружаем доступные расписания для выбора
                LoadAvailableSchedules();
            }
            else
            {
                MessageBox.Show("Запись не найдена", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }
        }

        private void LoadAvailableSchedules()
        {
            // Получаем все активные занятия с той же услугой
            var availableSchedules = _dbContext.Schedules
                .Where(s => s.SeasonticketServiceID == _waitingListClient.WaitingLists.SeasonticketServiceID &&
                       s.ScheduleStatus == "Активно" &&
                       s.StartDateTime > DateTime.Now)
                .OrderBy(s => s.StartDateTime)
                .ToList();

            SchedulesComboBox.ItemsSource = availableSchedules;
            SchedulesComboBox.DisplayMemberPath = "StartDateTime";
            SchedulesComboBox.SelectedValuePath = "ScheduleID";
        }

        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (SchedulesComboBox.SelectedItem is Schedules selectedSchedule)
            {
                try
                {
                    // Обновляем запись клиента в списке ожидания
                    _waitingListClient.IsProcessed = true;
                    _waitingListClient.Notes += $"\nОбработано: {DateTime.Now:dd.MM.yyyy HH:mm}. Назначено на {selectedSchedule.StartDateTime:dd.MM.yyyy HH:mm}";

                    // Назначаем клиента на выбранное занятие
                    selectedSchedule.ClientID = _waitingListClient.ClientID;

                    _dbContext.SaveChanges();

                    MessageBox.Show("Клиент успешно назначен на занятие", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при обработке записи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Выберите занятие для назначения клиента", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Отмечаем запись как обработанную, но без назначения на занятие
                _waitingListClient.IsProcessed = true;
                _waitingListClient.Notes += $"\nОтменено: {DateTime.Now:dd.MM.yyyy HH:mm}. Причина: {NotesTextBox.Text}";

                _dbContext.SaveChanges();

                MessageBox.Show("Запись отменена", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отмене записи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

}
