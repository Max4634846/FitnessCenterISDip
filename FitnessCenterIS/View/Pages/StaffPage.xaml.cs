using FitnessCenterIS.Model;
using FitnessCenterIS.View.Windows;
using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using StaffsCollection = FitnessCenterIS.Model.StaffsCollection;

namespace FitnessCenterIS.View.Pages
{
    public partial class StaffPage : Page
    {
        private readonly BDFitnessClubDipEntities _dbContext = new BDFitnessClubDipEntities();
        private ObservableCollection<StaffsCollection> _staffData;

        public StaffPage()
        {
            InitializeComponent();
            LoadStaffData();
        }

        private void LoadStaffData()
        {
            _staffData = new ObservableCollection<StaffsCollection>(
                _dbContext.Staffs
                    .Include(s => s.Persons)
                    .Include(s => s.Roles)
                    //.Where(s => !new[] { "Системный администратор", "Администратор стойки" }.Contains(s.Roles.Name))
                    .Select(s => new StaffsCollection
                    {
                        StaffID = s.StaffID,
                        Surname = s.Persons.Surname,
                        Name = s.Persons.Name,
                        MiddleName = s.Persons.MiddleName,
                        DateOfBirth = s.Persons.DateOfBirth,
                        Gender = s.Persons.Gender,
                        Role = s.Roles.Name,
                        PhoneNumber = s.Persons.PhoneNumber,
                        Email = s.Persons.Email,
                        PhotoPerson = s.Persons.ImagePerson,
                        QRCode = s.Persons.QRCode
                    })
            );

            BDPeople.ItemsSource = _staffData;
        }

        private void OpenEditWindow_Click(object sender, RoutedEventArgs e)
        {
            if (BDPeople.SelectedItem is StaffsCollection selectedStaff)
            {
                var window = new AddEditNewStaffWindow(selectedStaff.StaffID);
                if (window.ShowDialog() == true)
                    LoadStaffData();
            }
        }

        private void AddNewStaff_Click(object sender, RoutedEventArgs e)
        {
            var window = new AddEditNewStaffWindow();
            if (window.ShowDialog() == true)
            LoadStaffData();
        }

        private void DeleteStaff_Click(object sender, RoutedEventArgs e)
        {
            var selectedStaff = BDPeople.SelectedItem as StaffsCollection;
            if (selectedStaff == null)
            {
                MessageBox.Show("Выберите сотрудника для удаления");
                return;
            }

            if (MessageBox.Show("Удалить выбранного сотрудника?", "Подтверждение",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                using (var context = new BDFitnessClubDipEntities())
                {
                    var staff = context.Staffs.Find(selectedStaff.StaffID);
                    var person = context.Persons.Find(staff.PersonID);

                    if (staff != null) context.Staffs.Remove(staff);
                    if (person != null) context.Persons.Remove(person);

                    context.SaveChanges();
                    LoadStaffData();
                }
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text.ToLower();
            var filtered = _staffData.Where(s =>
                s.Surname.ToLower().Contains(searchText) ||
                s.Name.ToLower().Contains(searchText) ||
                s.MiddleName.ToLower().Contains(searchText)
            );
            BDPeople.ItemsSource = new ObservableCollection<StaffsCollection>(filtered);
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DataGridRow row = sender as DataGridRow;
            if (row != null && row.Item is StaffsCollection selectedClient)
            {
                int clientId = selectedClient.StaffID;
                ProfileStaff profilePage = new ProfileStaff(clientId);
                this.NavigationService?.Navigate(profilePage);
            }
        }

        private void BDStaff_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            if (e.Row != null)
            {
                e.Row.MouseDoubleClick += DataGridRow_MouseDoubleClick;
            }
        }

        private void BDStaff_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void ClientsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {

        }

        private void SendQRCodeButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedClient = BDPeople.SelectedItem as StaffsCollection;
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

        }

        private void OpenWinClient_Click(object sender, RoutedEventArgs e)
        {

        }

        private void EditClient_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DeleteClient_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
