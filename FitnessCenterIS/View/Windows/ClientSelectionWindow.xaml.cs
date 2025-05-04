using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Data.Entity;
using FitnessCenterIS.Model;

namespace FitnessCenterIS.View.Windows
{
    public partial class ClientSelectionWindow : Window
    {
        private BDFitnessClubDipEntities _context;
        private ObservableCollection<ClientViewModel> _clients;
        private CollectionViewSource _clientsViewSource;

        public int SelectedClientId { get; private set; }

        public ClientSelectionWindow()
        {
            InitializeComponent();
            _context = new BDFitnessClubDipEntities();
            _clients = new ObservableCollection<ClientViewModel>();

            _clientsViewSource = new CollectionViewSource();
            _clientsViewSource.Source = _clients;

            ClientsDataGrid.ItemsSource = _clientsViewSource.View;

            this.Loaded += (s, e) => LoadClients();
        }

        private void LoadClients()
        {
            _clients.Clear();

            var clientsData = _context.Clients
                .Include(c => c.Persons)
                .OrderBy(c => c.Persons.Surname)
                .ThenBy(c => c.Persons.Name)
                .ToList();

            foreach (var client in clientsData)
            {
                _clients.Add(new ClientViewModel
                {
                    ClientID = client.ClientID,
                    FullName = $"{client.Persons.Surname} {client.Persons.Name} {client.Persons.MiddleName}",
                    CardNumber = client.Persons.NumberCard,
                    PhoneNumber = client.Persons.PhoneNumber,
                    BonusPoints = client.BonuseBalance ?? 0
                });
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower();

            _clientsViewSource.View.Filter = item =>
            {
                if (item is ClientViewModel client)
                {
                    return string.IsNullOrWhiteSpace(searchText) ||
                           client.FullName.ToLower().Contains(searchText) ||
                           client.CardNumber?.ToLower().Contains(searchText) == true ||
                           client.PhoneNumber?.ToLower().Contains(searchText) == true;
                }
                return false;
            };
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Clear();
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClientsDataGrid.SelectedItem is ClientViewModel selectedClient)
            {
                SelectedClientId = selectedClient.ClientID;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите клиента из списка.",
                    "Клиент не выбран", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ClientsDataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (ClientsDataGrid.SelectedItem is ClientViewModel)
            {
                SelectButton_Click(sender, e);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }

    public class ClientViewModel
    {
        public int ClientID { get; set; }
        public string FullName { get; set; }
        public string CardNumber { get; set; }
        public string PhoneNumber { get; set; }
        public decimal BonusPoints { get; set; }
    }
}