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
using System.Windows.Navigation;
using System.Windows.Shapes;
using FitnessCenterIS.View.Windows;

namespace FitnessCenterIS.View.Pages
{
    /// <summary>
    /// Interaction logic for WaitingListPage.xaml
    /// </summary>
    public partial class WaitingListPage : Page
    {
        private BDFitnessClubDipEntities _dbContext;
        private List<WaitingListViewModel> _waitingListItems;

        public WaitingListPage()
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadWaitingList();
        }

        private void LoadWaitingList()
        {
            var waitingListClients = _dbContext.WaitingListClients
                .Include("WaitingLists")
                .Include("WaitingLists.SeasonticketServices.Services")
                .Include("Clients.Persons")
                .ToList();

            _waitingListItems = waitingListClients.Select(wlc => new WaitingListViewModel
            {
                WaitingListID = (int)wlc.WaitingListID,
                WaitingID = wlc.WaitingID,
                ClientID = (int)wlc.ClientID,
                ClientName = $"{wlc.Clients.Persons.Surname} {wlc.Clients.Persons.Name}",
                ServiceName = wlc.WaitingLists.SeasonticketServices.Services.Name,
                EnrollmentDateTime = (DateTime)wlc.EnrollmentDateTime,
                IsProcessed = (bool)wlc.IsProcessed,
                Status = (bool)wlc.IsProcessed ? "Обработан" : "Ожидает",
                Notes = wlc.Notes
            }).ToList();

            WaitingListDataGrid.ItemsSource = _waitingListItems;
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadWaitingList();
        }

        private void WaitingListDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Можно добавить логику для отображения деталей выбранной записи
        }

        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.DataContext is WaitingListViewModel selectedItem)
            {
                // Открываем окно для обработки записи из списка ожидания
                var processWindow = new ProcessWaitingListWindow(_dbContext, selectedItem.WaitingID);
                if (processWindow.ShowDialog() == true)
                {
                    LoadWaitingList();
                }
            }
        }
    }

    public class WaitingListViewModel
    {
        public int WaitingListID { get; set; }
        public int WaitingID { get; set; }
        public int ClientID { get; set; }
        public string ClientName { get; set; }
        public string ServiceName { get; set; }
        public DateTime EnrollmentDateTime { get; set; }
        public bool IsProcessed { get; set; }
        public string Status { get; set; }
        public string Notes { get; set; }
    }

}
