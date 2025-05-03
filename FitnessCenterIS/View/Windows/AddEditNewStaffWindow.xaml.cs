using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;

namespace FitnessCenterIS.View.Windows
{
    public partial class AddEditNewStaffWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private bool _isEditMode = false;
        private int _staffId;

        public AddEditNewStaffWindow()
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadRoles();

            EditBtn.Visibility = Visibility.Collapsed;
            IdStaff.Visibility = Visibility.Collapsed;
            IdStaffLabel.Visibility = Visibility.Collapsed;
        }

        public AddEditNewStaffWindow(int staffId)
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadRoles();
            _isEditMode = true;
            _staffId = staffId;
            AddBtn.Visibility = Visibility.Collapsed;
            EditBtn.Visibility = Visibility.Visible;
            IdStaff.Visibility = Visibility.Visible;
            IdStaffLabel.Visibility = Visibility.Visible;
            IdStaff.Text = staffId.ToString();
            LoadStaffData(staffId);
        }

        private void LoadRoles()
        {
            RoleComboBox.ItemsSource = _dbContext.Roles.ToList();
            RoleComboBox.DisplayMemberPath = "Name";
            RoleComboBox.SelectedValuePath = "RoleID";
        }

        private void LoadStaffData(int staffId)
        {
            var staff = _dbContext.Staffs.FirstOrDefault(s => s.StaffID == staffId);
            if (staff != null)
            {
                var person = _dbContext.Persons.FirstOrDefault(p => p.PersonID == staff.PersonID);
                if (person != null)
                {
                    FirstNameTextBox.Text = person.Name;
                    LastNameTextBox.Text = person.Surname;
                    MiddleNameTextBox.Text = person.MiddleName;
                    PhoneNumberTextBox.Text = person.PhoneNumber;
                    EmailTextBox.Text = person.Email;
                    DateOfBirthPicker.SelectedDate = person.DateOfBirth;
                    AddressTextBox.Text = person.Address;
                    Notes.Text = person.Notes;
                    NumberCardTextBox.Text = person.NumberCard;

                    // Пол
                    if (!string.IsNullOrEmpty(person.Gender))
                    {
                        foreach (ComboBoxItem item in GenderComboBox.Items)
                        {
                            if (item.Content.ToString() == person.Gender)
                            {
                                GenderComboBox.SelectedItem = item;
                                break;
                            }
                        }
                    }
                    // Фото профиля
                    if (!string.IsNullOrEmpty(person.ImagePerson))
                    {
                        try
                        {
                            StaffImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(person.ImagePerson));
                            StaffImage.Tag = person.ImagePerson;
                        }
                        catch
                        {
                            StaffImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("/Resource/NewPerson.jpg", UriKind.Relative));
                            StaffImage.Tag = null;
                        }
                    }
                    else
                    {
                        StaffImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("/Resource/NewPerson.jpg", UriKind.Relative));
                        StaffImage.Tag = null;
                    }
                    // QR-код
                    if (!string.IsNullOrEmpty(person.QRCode) && File.Exists(person.QRCode))
                    {
                        QRCodeImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(person.QRCode));
                        QRCodeImage.Tag = person.QRCode;
                    }
                    else
                    {
                        QRCodeImage.Source = null;
                        QRCodeImage.Tag = null;
                    }
                }
                if (staff.RoleID.HasValue)
                    RoleComboBox.SelectedValue = staff.RoleID;
                if (!string.IsNullOrEmpty(staff.HireDate))
                {
                    DateTime hireDate;
                    if (DateTime.TryParse(staff.HireDate, out hireDate))
                        HireDatePicker.SelectedDate = hireDate;
                }
            }
        }

        private void BtnImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png)|*.jpg;*.jpeg;*.png|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                StaffImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(filePath));
                StaffImage.Tag = filePath;
            }
        }

        private void GenerateCardButton_Click(object sender, RoutedEventArgs e)
        {
            string newCard;
            do
            {
                // Генерируем номер карты только из цифр
                Random random = new Random();
                newCard = random.Next(1000, 9999).ToString() + random.Next(10000, 99999).ToString();
            }
            while (_dbContext.Persons.Any(p => p.NumberCard == newCard));

            NumberCardTextBox.Text = newCard;
        }

        private void NumberCardTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string cardNumber = NumberCardTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(cardNumber))
            {
                string qrPath = GenerateAndSaveQRCode(cardNumber);
                if (File.Exists(qrPath))
                {
                    QRCodeImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(qrPath));
                    QRCodeImage.Tag = qrPath;
                }
                else
                {
                    QRCodeImage.Source = null;
                    QRCodeImage.Tag = null;
                }
            }
            else
            {
                QRCodeImage.Source = null;
                QRCodeImage.Tag = null;
            }
        }

        private string GenerateAndSaveQRCode(string data)
        {
            if (string.IsNullOrWhiteSpace(data))
                return null;

            string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QRCodes");
            Directory.CreateDirectory(folderPath);

            // Удаляем все недопустимые символы из имени файла
            string safeFileName = string.Concat(data.Where(c => !Path.GetInvalidFileNameChars().Contains(c)));
            string fileName = $"StaffQRCode_{safeFileName}_{Guid.NewGuid():N}.png";
            string filePath = Path.Combine(folderPath, fileName);

            try
            {
                using (var qrGenerator = new QRCodeGenerator())
                using (var qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q))
                using (var qrCode = new QRCode(qrCodeData))
                using (Bitmap qrCodeImage = qrCode.GetGraphic(20))
                {
                    qrCodeImage.Save(filePath, ImageFormat.Png);
                }
                return filePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении QR-кода: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }


        private void AddStaffButton_Click(object sender, RoutedEventArgs e)
        {
            string firstName = FirstNameTextBox.Text.Trim();
            string lastName = LastNameTextBox.Text.Trim();
            string middleName = MiddleNameTextBox.Text.Trim();
            string phoneNumber = PhoneNumberTextBox.Text.Trim();
            string email = EmailTextBox.Text.Trim();
            DateTime? dateOfBirth = DateOfBirthPicker.SelectedDate;
            string address = AddressTextBox.Text.Trim();
            string gender = (GenderComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string notes = Notes.Text.Trim();
            string imagePath = StaffImage.Tag as string;
            int? roleId = RoleComboBox.SelectedValue as int?;
            DateTime? hireDate = HireDatePicker.SelectedDate;
            string numberCard = NumberCardTextBox.Text.Trim();
            string qrCodePath = QRCodeImage.Tag as string;

            // Проверка обязательных полей
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || dateOfBirth == null || roleId == null || hireDate == null)
            {
                MessageBox.Show("Пожалуйста, заполните все обязательные поля.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверка уникальности номера карты
            if (_dbContext.Persons.Any(p => p.NumberCard == numberCard && (!_isEditMode || p.Staffs.FirstOrDefault().StaffID != _staffId)))
            {
                MessageBox.Show($"Сотрудник или клиент с номером карты '{numberCard}' уже существует.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_isEditMode)
            {
                var staff = _dbContext.Staffs.FirstOrDefault(s => s.StaffID == _staffId);
                if (staff == null)
                {
                    MessageBox.Show("Сотрудник не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var person = _dbContext.Persons.FirstOrDefault(p => p.PersonID == staff.PersonID);
                if (person == null)
                {
                    MessageBox.Show("Персональные данные сотрудника не найдены.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                person.Surname = lastName;
                person.Name = firstName;
                person.MiddleName = middleName;
                person.PhoneNumber = phoneNumber;
                person.Email = email;
                person.DateOfBirth = dateOfBirth;
                person.Address = address;
                person.Gender = gender;
                person.Notes = notes;
                person.ImagePerson = imagePath;
                person.NumberCard = numberCard;
                person.QRCode = qrCodePath;

                staff.RoleID = roleId;
                staff.HireDate = hireDate.Value.ToString("yyyy-MM-dd");

                _dbContext.SaveChanges();
                MessageBox.Show("Данные сотрудника успешно обновлены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
            }
            else
            {
                var person = new Persons
                {
                    Surname = lastName,
                    Name = firstName,
                    MiddleName = middleName,
                    PhoneNumber = phoneNumber,
                    Email = email,
                    DateOfBirth = dateOfBirth,
                    Address = address,
                    Gender = gender,
                    Notes = notes,
                    ImagePerson = imagePath,
                    NumberCard = numberCard,
                    QRCode = qrCodePath
                };
                _dbContext.Persons.Add(person);
                _dbContext.SaveChanges();

                var staff = new Staffs
                {
                    PersonID = person.PersonID,
                    RoleID = roleId,
                    HireDate = hireDate.Value.ToString("yyyy-MM-dd")
                };
                _dbContext.Staffs.Add(staff);
                _dbContext.SaveChanges();

                MessageBox.Show("Сотрудник успешно добавлен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
