using FitnessCenterIS.Model;
using FitnessCenterIS.View.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
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

namespace FitnessCenterIS.View.Pages
{
    /// <summary>
    /// Interaction logic for ClientPage.xaml
    /// </summary>
    public partial class ClientPage : Page
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private MenuWindow _menuWindow;
        private List<ClientsCollection> _allClientsData;

        public ClientPage(MenuWindow menuWindow)
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            _menuWindow = menuWindow; // Store the MenuWindow instance
            UpdateBDPeople();
        }

        public void UpdateBDPeople()
        {
            var clientsData = _dbContext.Clients
                .Where(client => client.StatusClient != "Лид")
                .Join(_dbContext.Persons,
                    client => client.PersonID,
                    person => person.PersonID,
                    (client, person) => new ClientsCollection
                    {
                        ClientID = client.ClientID,
                        Surname = person.Surname,
                        Name = person.Name,
                        MiddleName = person.MiddleName,
                        DateOfBirth = person.DateOfBirth,
                        Gender = person.Gender,
                        PhoneNumber = person.PhoneNumber,
                        Email = person.Email,
                        QRCode = client.QRCode,
                    })
                .ToList();

            _allClientsData = clientsData;
            BDPeople.ItemsSource = clientsData;
        }


        private void TextBox_Changed(object sender, TextChangedEventArgs e)
        {

        }

        private void SendQRCodeButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = BDPeople.SelectedItem as ClientsCollection;
            if (selectedClient == null)
            {
                MessageBox.Show("Пожалуйста, выберите клиента для отправки QR-кода.");
                return;
            }
            if (string.IsNullOrEmpty(selectedClient.Email))
            {
                MessageBox.Show("У выбранного клиента отсутствует адрес электронной почты.");
                return;
            }
            if (string.IsNullOrEmpty(selectedClient.QRCode) || !File.Exists(selectedClient.QRCode))
            {
                MessageBox.Show("QR-код для выбранного клиента отсутствует или не найден.");
                return;
            }


            try
            {
                using (var memoryStream = new MemoryStream(File.ReadAllBytes(selectedClient.QRCode)))
                {
                    SendEmailWithAttachment(selectedClient.Email, memoryStream);
                    MessageBox.Show("QR-код успешно отправлен на почту клиента.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при отправке письма: " + ex.Message);
            }
        }

        private void SendEmailWithAttachment(string email, MemoryStream attachmentStream)
        {
            try
            {
                var smtpClient = new SmtpClient("smtp.mail.ru")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("fitness.clublive@mail.ru", "0iHFGPSQk2mqQGyejNCb"),
                    EnableSsl = true,
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress("fitness.clublive@mail.ru"),
                    Subject = "Ваш QR-код для посещения фитнес-клуба",
                    Body = "Добрый день! В приложении вы найдете ваш QR-код для доступа к нашему фитнес-клубу.",
                    IsBodyHtml = false,
                };
                mailMessage.To.Add(email);

                attachmentStream.Position = 0;
                var attachment = new Attachment(attachmentStream, "QRCode.png", "image/png");
                mailMessage.Attachments.Add(attachment);

                smtpClient.Send(mailMessage);
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при отправке письма: {ex.Message}");
            }
        }

        private void QRCodeOpenScan_Click(object sender, RoutedEventArgs e)
        {
            // Fetch Clients entities from the database
            var clientsList = _dbContext.Clients
                .Where(client => client.StatusClient != "Лид")
                .Include(c => c.Persons) // Include Persons if VisitClientWindow needs it
                .ToList();

            ObservableCollection<Clients> clientsObservableCollection = new ObservableCollection<Clients>(clientsList);

            VisitClientWindow visitClientWindow = new VisitClientWindow(clientsObservableCollection, _menuWindow);
            visitClientWindow.ShowDialog();
        }

        private void AddTicBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void BDPeople_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row != null)
            {
                e.Row.MouseDoubleClick += ClientsDataGrid_MouseDoubleClick;
            }
        }

        private void BDPeople_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void OpenWinClient_Click(object sender, RoutedEventArgs e)
        {
            Windows.AddEditNewClientWindow addEditNewClientWindow = new Windows.AddEditNewClientWindow();
            addEditNewClientWindow.ShowDialog();
            UpdateBDPeople();
        }

        private void GroupBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void EditClient_Click(object sender, RoutedEventArgs e)
        {
            if (BDPeople.SelectedItem is ClientsCollection selectedClient)
            {
                Windows.AddEditNewClientWindow editWindow = new Windows.AddEditNewClientWindow(selectedClient.ClientID);
                editWindow.ShowDialog();
                UpdateBDPeople();
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите клиента для редактирования.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void DeleteClient_Click(object sender, RoutedEventArgs e)
        {
            var selectedClientsCollection = BDPeople.SelectedItems.Cast<ClientsCollection>().ToList();

            if (selectedClientsCollection.Count == 0)
            {
                MessageBox.Show("Пожалуйста, выберите клиента для удаления.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Вы действительно хотите удалить {selectedClientsCollection.Count()} выбранных клиентов и все связанные с ними данные (включая данные о клиенте и опекунах)?", "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    using (var context = new BDFitnessClubDipEntities())
                    {
                        foreach (var clientCollection in selectedClientsCollection)
                        {
                            // Находим клиента в таблице Clients по ClientID
                            var clientToDelete = context.Clients.FirstOrDefault(c => c.ClientID == clientCollection.ClientID);

                            if (clientToDelete != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Удаление клиента с ID: {clientToDelete.ClientID}");

                                // Получаем PersonID клиента перед удалением из Clients
                                int personIdToDelete = clientToDelete.Persons.PersonID;

                                // Находим и удаляем связанные записи из таблицы Guardianships
                                var guardianshipsToDelete = context.Guardianships.Where(g => g.ClientID == clientToDelete.ClientID).ToList();
                                System.Diagnostics.Debug.WriteLine($"Найдено {guardianshipsToDelete.Count} записей об опекунстве для ClientID: {clientToDelete.ClientID}");

                                // Собираем PersonID опекунов для удаления
                                var guardianPersonIdsToDelete = guardianshipsToDelete.Where(g => g.ResponsiblePersonID.HasValue)
                                                                                                 .Select(g => g.ResponsiblePersonID.Value)
                                                                                                 .ToList();

                                context.Guardianships.RemoveRange(guardianshipsToDelete);

                                // Удаляем связанные записи из таблицы Sales
                                var salesToDelete = context.Sales.Where(s => s.Seasontickets.SeasonticketClients.Any(sc => sc.ClientID == clientToDelete.ClientID)).ToList();


                                // Удаляем связанные записи из таблицы Tasks
                                var tasksToDelete = context.Tasks.Where(t => t.ClientID == clientToDelete.ClientID).ToList();
                                context.Tasks.RemoveRange(tasksToDelete);

                                // Удаляем клиента из таблицы Clients
                                context.Clients.Remove(clientToDelete);

                                // Находим и удаляем запись клиента из таблицы Persons
                                var personToDelete = context.Persons.FirstOrDefault(p => p.PersonID == personIdToDelete);
                                if (personToDelete != null)
                                {
                                    context.Persons.Remove(personToDelete);
                                    System.Diagnostics.Debug.WriteLine($"Удалена запись клиента из Persons с ID: {personToDelete.PersonID}");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"Запись клиента из Persons с ID: {personIdToDelete} не найдена.");
                                }

                                // Удаляем данные об опекунах из Persons
                                foreach (var guardianPersonId in guardianPersonIdsToDelete.Distinct()) // Обрабатываем только уникальные PersonID опекунов
                                {
                                    var guardianPersonToDelete = context.Persons.FirstOrDefault(p => p.PersonID == guardianPersonId);
                                    if (guardianPersonToDelete != null)
                                    {
                                        context.Persons.Remove(guardianPersonToDelete);
                                        System.Diagnostics.Debug.WriteLine($"Удалена запись опекуна из Persons с ID: {guardianPersonToDelete.PersonID}");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Запись опекуна из Persons с ID: {guardianPersonId} не найдена.");
                                    }
                                }
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"Клиент с ID: {clientCollection.ClientID} не найден в таблице Clients.");
                            }
                        }

                        context.SaveChanges();
                        MessageBox.Show("Выбранные клиенты и все связанные с ними данные успешно удалены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        UpdateBDPeople(); // Обновляем DataGrid после удаления
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Произошла ошибка при удалении: {ex.Message}");
                    if (ex.InnerException != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Внутреннее исключение: {ex.InnerException.Message}");
                    }
                    MessageBox.Show($"Произошла ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void CloakroomBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void TaskBtn_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ClientsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridRow row = sender as DataGridRow;
            if (row != null && row.Item is ClientsCollection selectedClient)
            {
                int clientId = selectedClient.ClientID;
                ProfileClient profilePage = new ProfileClient(clientId);
                this.NavigationService?.Navigate(profilePage);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower();

            var filteredClients = _allClientsData.Where(client =>
                client.Surname.ToLower().Contains(searchText) ||
                client.Name.ToLower().Contains(searchText) ||
                client.MiddleName.ToLower().Contains(searchText) ||
                (client.DateOfBirth?.ToString("dd.MM.yyyy") ?? "").Contains(searchText)
            ).ToList();

            BDPeople.ItemsSource = filteredClients;
        }
    }
}