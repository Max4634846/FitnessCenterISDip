using FitnessCenterIS.Model;
using FitnessCenterIS.View.Pages;
using FitnessCenterIS.View.Windows; // Namespace for QRCodeWin
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Data.Entity; // Необходимо для Include

namespace FitnessCenterIS.View.Windows
{
    /// <summary>
    /// Interaction logic for VisitClientWindow.xaml
    /// </summary>
    public partial class VisitClientWindow : Window
    {
        private ObservableCollection<Clients> _clientList;
        private MenuWindow _menuWindow; // Добавляем поле для ссылки на MenuWindow

        public VisitClientWindow(ObservableCollection<Clients> clientList, MenuWindow menuWindow)
        {
            InitializeComponent();
            _clientList = clientList;
            _menuWindow = menuWindow; // Сохраняем переданную ссылку
        }

        private void FindClientByCardNumber_Click(object sender, RoutedEventArgs e)
        {
            string cardNumber = CardNumberTextBox.Text;
            if (!string.IsNullOrEmpty(cardNumber))
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    var client = context.Clients
                        .Include(c => c.Persons) // Ensure Persons are loaded if needed on ProfileClient
                        .FirstOrDefault(c => c.NumberCard == cardNumber);
                    if (client != null)
                    {
                        var profileClientPage = new ProfileClient(client.ClientID);
                        _menuWindow.MainFrame.Navigate(profileClientPage);
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show($"Клиент с номером карты {cardNumber} не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Пожалуйста, введите номер карты.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StartQRCodeScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_clientList != null && _clientList.Any())
            {
                var clientsCollectionList = _clientList.Select(c => new ClientsCollection
                {
                    ClientID = c.ClientID,
                    FullName = c.Persons?.Surname + " " + c.Persons?.Name + " " + c.Persons?.MiddleName,
                    // Add other properties as needed for ClientsCollection
                }).ToList(); // Use ObservableCollection here

                QRCodeWindow scanWindow = new QRCodeWindow(clientsCollectionList, _menuWindow); // Передаем _menuWindow
                scanWindow.QRCodeScanned += ScanWindow_QRCodeScanned;

                this.Close();
                scanWindow.ShowDialog(); // Открываем окно сканера как модальное
            }
            else
            {
                MessageBox.Show("Список клиентов не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ScanWindow_QRCodeScanned(string cardNumber) // Изменено: принимаем номер карты
        {
            if (!string.IsNullOrEmpty(cardNumber))
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    var client = context.Clients
                        .Include(c => c.Persons) // Ensure Persons are loaded if needed on ProfileClient
                        .FirstOrDefault(c => c.NumberCard == cardNumber);

                    if (client != null)
                    {
                        var profileClientPage = new ProfileClient(client.ClientID);
                        _menuWindow.MainFrame.Navigate(profileClientPage);
                        this.Close();
                    }
                    else
                    {
                        MessageBox.Show($"Клиент с номером карты {cardNumber} не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Отсканированный QR-код не содержит номера карты.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}