using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using System.Text.RegularExpressions;
using QRCoder;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic; // Добавляем пространство имен для List

namespace FitnessCenterIS.View.Windows
{
    public partial class AddEditNewClientWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private bool _isEditMode = false;
        private int _clientId; // Для хранения ID клиента в режиме редактирования
        private List<Relationships> _relationships; // Добавляем поле для хранения типов родства

        public AddEditNewClientWindow(bool isLead = false)
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadRelationships(); // Загружаем типы родства при инициализации

            StatusClientComboBox.SelectedIndex = isLead ? 1 : 0;

            Title.Text = isLead ? "Новый лид" : "Новый клиент";
            AddBtn.Content = isLead ? "Добавить" : "Добавить";
            StatusCL.Content = isLead ? "Лид" : "Клиент";

            StatusClientComboBox.SelectedIndex = 0; // Устанавливаем "Клиент" по умолчанию
            EditBtn.Visibility = Visibility.Collapsed;
            IdClient.Visibility = Visibility.Collapsed; // Скрываем поле ID при добавлении
            IdClientLabel.Visibility = Visibility.Collapsed; // Скрываем поле ID при добавлении
            GuardianTabItem.Visibility = Visibility.Collapsed; // Скрываем вкладку "Опекун" по умолчанию
        }

        // Конструктор для режима редактирования
        public AddEditNewClientWindow(int clientId)
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            LoadRelationships(); // Загружаем типы родства при инициализации
            _isEditMode = true;
            _clientId = clientId;
            AddBtn.Content = "Сохранить изменения";
            EditBtn.Visibility = Visibility.Visible;
            AddBtn.Visibility = Visibility.Collapsed;
            IdClient.Visibility = Visibility.Visible; // Показываем поле ID при редактировании
            IdClientLabel.Visibility = Visibility.Visible; // Показываем поле ID при редактировании
            IdClient.Text = clientId.ToString();
            LoadClientData(clientId);
            // GuardianTabItem.Visibility = Visibility.Collapsed; // Больше не скрываем здесь, логика в LoadClientData и DateOfBithTextBox_SelectedDateChanged
        }

        private void LoadRelationships()
        {
            try
            {
                _relationships = _dbContext.Relationships.ToList();
                GuardianRelationshipComboBox.ItemsSource = _relationships;
                GuardianRelationshipComboBox.DisplayMemberPath = "Name";
                GuardianRelationshipComboBox.SelectedValuePath = "RelationshipID";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке типов родства: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadClientData(int clientId)
        {
            try
            {
                var client = _dbContext.Clients.FirstOrDefault(c => c.ClientID == clientId);
                if (client != null)
                {
                    var person = _dbContext.Persons.FirstOrDefault(p => p.PersonID == client.PersonID);
                    if (person != null)
                    {
                        FirstNameTextBox.Text = person.Name;
                        LastNameTextBox.Text = person.Surname;
                        MiddleNameTextBox.Text = person.MiddleName;
                        PhoneNumberTextBox.Text = person.PhoneNumber;
                        EmailTextBox.Text = person.Email;
                        DateOfBithTextBox.SelectedDate = person.DateOfBirth;
                        AddressTextBox.Text = person.Address;

                        // Выбираем пол в ComboBox
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

                        Notes.Text = person.Notes;
                        // Загрузка изображения, если путь сохранен
                        if (!string.IsNullOrEmpty(person.ImagePerson))
                        {
                            try
                            {
                                ClientImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(person.ImagePerson));
                                ClientImage.Tag = person.ImagePerson;
                            }
                            catch
                            {
                                ClientImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("/Resource/NewPerson.jpg", UriKind.Relative));
                                ClientImage.Tag = null;
                            }
                        }
                        else
                        {
                            ClientImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("/Resource/NewPerson.jpg", UriKind.Relative));
                            ClientImage.Tag = null;
                        }

                        // Выбираем статус клиента
                        if (!string.IsNullOrEmpty(client.StatusClient))
                        {
                            foreach (ComboBoxItem item in StatusClientComboBox.Items)
                            {
                                if (item.Content.ToString() == client.StatusClient)
                                {
                                    StatusClientComboBox.SelectedItem = item;
                                    break;
                                }
                            }
                        }

                        NumberCardTextBox.Text = client.NumberCard;

                        // Загрузка данных об опекуне, если есть
                        if (person.DateOfBirth.HasValue)
                        {
                            DateTime today = DateTime.Today;
                            int age = today.Year - person.DateOfBirth.Value.Year;
                            if (person.DateOfBirth > today.AddYears(-age))
                                age--;
                            GuardianTabItem.Visibility = age < 18 ? Visibility.Visible : Visibility.Collapsed;

                            if (age < 18)
                            {
                                var guardianship = _dbContext.Guardianships.FirstOrDefault(g => g.ClientID == clientId);
                                if (guardianship != null)
                                {
                                    var guardianPerson = _dbContext.Persons.FirstOrDefault(p => p.PersonID == guardianship.ResponsiblePersonID);
                                    var relationship = _dbContext.Relationships.FirstOrDefault(r => r.RelationshipID == guardianship.RelationshipID);

                                    if (guardianPerson != null)
                                    {
                                        GuardianLastNameTextBox.Text = guardianPerson.Surname;
                                        GuardianFirstNameTextBox.Text = guardianPerson.Name;
                                        GuardianMiddleNameTextBox.Text = guardianPerson.MiddleName;
                                        GuardianPhoneTextBox.Text = guardianPerson.PhoneNumber;
                                        GuardianEmailTextBox.Text = guardianPerson.Email;
                                    }
                                    if (relationship != null)
                                    {
                                        GuardianRelationshipComboBox.SelectedValue = relationship.RelationshipID;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Клиент не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных клиента: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnImage_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.png)|*.jpg;*.jpeg;*.png|All files (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName;
                ClientImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(filePath));
                ClientImage.Tag = filePath;
            }
        }

        private void AddClientButton_Click(object sender, RoutedEventArgs e)
        {
            string firstName = FirstNameTextBox.Text.Trim();
            string lastName = LastNameTextBox.Text.Trim();
            string middleName = MiddleNameTextBox.Text.Trim();
            string phoneNumber = PhoneNumberTextBox.Text.Trim();
            string email = EmailTextBox.Text.Trim();
            DateTime? dateOfBirth = DateOfBithTextBox.SelectedDate;
            string address = AddressTextBox.Text.Trim();
            string gender = (GenderComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string notes = Notes.Text.Trim();
            string imagePath = ClientImage.Tag as string;
            string statusClient = (StatusClientComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            string numberCard = NumberCardTextBox.Text.Trim();

            // Проверка обязательных полей
            if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || dateOfBirth == null)
            {
                MessageBox.Show("Пожалуйста, заполните имя, фамилию и дату рождения.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Валидация email
            if (!IsValidEmail(email))
            {
                MessageBox.Show("Некорректный формат email. Пример: client@example.com", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Валидация телефона
            if (!string.IsNullOrWhiteSpace(phoneNumber) && !IsValidPhone(phoneNumber))
            {
                MessageBox.Show("Некорректный формат телефона. Пример: +79XXXXXXXXX", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_isEditMode)
            {
                // Режим редактирования
                try
                {
                    var existingClient = _dbContext.Clients.FirstOrDefault(c => c.ClientID == _clientId);
                    var existingPerson = _dbContext.Persons.FirstOrDefault(p => p.PersonID == existingClient.PersonID);

                    if (existingClient != null && existingPerson != null)
                    {
                        existingPerson.Surname = lastName.Length > 0 ? lastName : null;
                        existingPerson.Name = firstName.Length > 0 ? firstName : null;
                        existingPerson.MiddleName = middleName.Length > 0 ? middleName : null;
                        existingPerson.Email = email.Length > 0 ? email : null;
                        existingPerson.PhoneNumber = phoneNumber.Length > 0 ? phoneNumber : null;
                        existingPerson.DateOfBirth = dateOfBirth;
                        existingPerson.ImagePerson = imagePath?.Length > 0 ? imagePath : null;
                        existingPerson.Address = address.Length > 0 ? address : null;
                        existingPerson.Gender = gender?.Length > 0 ? gender : null;
                        existingPerson.Notes = notes.Length > 0 ? notes : null;

                        existingClient.StatusClient = statusClient?.Length > 0 ? statusClient : null;
                        if (existingClient.NumberCard != numberCard)
                        {
                            if (_dbContext.Clients.Any(c => c.NumberCard == numberCard && c.ClientID != _clientId))
                            {
                                MessageBox.Show($"Клиент с номером карты '{numberCard}' уже существует в базе данных.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            existingClient.NumberCard = numberCard;
                            existingClient.QRCode = GenerateQRCodeBase64(numberCard); // Обновляем QR-код при изменении номера карты
                        }

                        // Обновление информации об опекуне, если клиент младше 18
                        if (existingPerson.DateOfBirth.HasValue)
                        {
                            DateTime birthDate = existingPerson.DateOfBirth.Value;
                            DateTime today = DateTime.Today;
                            int age = today.Year - birthDate.Year;
                            if (birthDate > today.AddYears(-age))
                                age--;

                            if (age < 18)
                            {
                                string guardianLastName = GuardianLastNameTextBox.Text.Trim();
                                string guardianFirstName = GuardianFirstNameTextBox.Text.Trim();
                                string guardianMiddleName = GuardianMiddleNameTextBox.Text.Trim();
                                string guardianPhone = GuardianPhoneTextBox.Text.Trim();
                                string guardianEmail = GuardianEmailTextBox.Text.Trim();
                                Relationships selectedRelationship = _relationships.FirstOrDefault(r => r.RelationshipID == (int?)GuardianRelationshipComboBox.SelectedValue);

                                if (selectedRelationship != null)
                                {
                                    var guardianship = _dbContext.Guardianships.FirstOrDefault(g => g.ClientID == _clientId);
                                    if (guardianship != null)
                                    {
                                        var guardianPerson = _dbContext.Persons.FirstOrDefault(p => p.PersonID == guardianship.ResponsiblePersonID);
                                        if (guardianPerson != null)
                                        {
                                            guardianPerson.Surname = guardianLastName;
                                            guardianPerson.Name = guardianFirstName;
                                            guardianPerson.MiddleName = guardianMiddleName;
                                            guardianPerson.PhoneNumber = guardianPhone;
                                            guardianPerson.Email = guardianEmail;
                                            guardianship.RelationshipID = selectedRelationship.RelationshipID;
                                        }
                                        else
                                        {
                                            // Возможно, опекуна еще не было, нужно создать
                                            var newGuardian = new Persons
                                            {
                                                Surname = guardianLastName,
                                                Name = guardianFirstName,
                                                MiddleName = guardianMiddleName,
                                                PhoneNumber = guardianPhone,
                                                Email = guardianEmail
                                            };
                                            _dbContext.Persons.Add(newGuardian);
                                            _dbContext.SaveChanges(); // Save to get the new GuardianID
                                            guardianship.ResponsiblePersonID = newGuardian.PersonID;
                                            guardianship.RelationshipID = selectedRelationship.RelationshipID;
                                        }
                                    }
                                    else
                                    {
                                        // Создаем новую запись опекунства
                                        var newGuardian = new Persons
                                        {
                                            Surname = guardianLastName,
                                            Name = guardianFirstName,
                                            MiddleName = guardianMiddleName,
                                            PhoneNumber = guardianPhone,
                                            Email = guardianEmail
                                        };
                                        _dbContext.Persons.Add(newGuardian);
                                        _dbContext.SaveChanges();
                                        var newGuardianship = new Guardianships
                                        {
                                            ClientID = _clientId,
                                            ResponsiblePersonID = newGuardian.PersonID,
                                            RelationshipID = selectedRelationship.RelationshipID
                                        };
                                        _dbContext.Guardianships.Add(newGuardianship);
                                    }
                                }
                            }
                            else
                            {
                                // Если клиент стал старше 18, удаляем информацию об опекуне
                                var existingGuardianship = _dbContext.Guardianships.FirstOrDefault(g => g.ClientID == _clientId);
                                if (existingGuardianship != null)
                                {
                                    var guardianPerson = _dbContext.Persons.FirstOrDefault(p => p.PersonID == existingGuardianship.ResponsiblePersonID);
                                    if (guardianPerson != null)
                                    {
                                        _dbContext.Persons.Remove(guardianPerson); // Consider if you should only remove the guardianship
                                    }
                                    _dbContext.Guardianships.Remove(existingGuardianship);
                                }
                            }
                        }

                        _dbContext.SaveChanges();
                        MessageBox.Show($"Данные клиента {firstName} {lastName} успешно обновлены.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        this.Close(); // Закрываем окно после редактирования
                    }
                    else
                    {
                        MessageBox.Show("Ошибка при редактировании клиента: запись не найдена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при обновлении данных клиента: {ex.Message}\n\n{ex.InnerException?.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Режим добавления нового клиента
                if (string.IsNullOrWhiteSpace(numberCard))
                {
                    MessageBox.Show("Пожалуйста, введите номер карты для генерации QR-кода.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (_dbContext.Clients.Any(c => c.NumberCard == numberCard))
                {
                    MessageBox.Show($"Клиент с номером карты '{numberCard}' уже существует в базе данных.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return; // Прерываем добавление клиента
                }

                if (!dateOfBirth.HasValue)
                {
                    MessageBox.Show("Пожалуйста, выберите дату рождения клиента.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DateTime birthDate = dateOfBirth.Value;
                DateTime today = DateTime.Today;
                int age = today.Year - birthDate.Year;
                if (birthDate > today.AddYears(-age))
                    age--;

                var newPerson = new Persons
                {
                    Surname = lastName.Length > 0 ? lastName : null,
                    Name = firstName.Length > 0 ? firstName : null,
                    MiddleName = middleName.Length > 0 ? middleName : null,
                    Email = email.Length > 0 ? email : null,
                    PhoneNumber = phoneNumber.Length > 0 ? phoneNumber : null,
                    DateOfBirth = dateOfBirth,
                    ImagePerson = imagePath?.Length > 0 ? imagePath : null,
                    Address = address.Length > 0 ? address : null,
                    Gender = gender?.Length > 0 ? gender : null,
                    Notes = notes.Length > 0 ? notes : null
                };

                _dbContext.Persons.Add(newPerson);
                _dbContext.SaveChanges();

                var newClient = new Clients
                {
                    PersonID = newPerson.PersonID,
                    BonuseBalance = 0,
                    DepositBalance = 0,
                    LoyaltyLevelID = null,
                    StatusClient = statusClient?.Length > 0 ? statusClient : null,
                    NumberCard = numberCard,
                    QRCode = GenerateQRCodeBase64(numberCard) // Сохраняем путь к файлу
                };

                _dbContext.Clients.Add(newClient);
                _dbContext.SaveChanges(); // Save client to get ClientID

                // Добавление опекуна, если клиент младше 18
                if (age < 18)
                {
                    string guardianLastName = GuardianLastNameTextBox.Text.Trim();
                    string guardianFirstName = GuardianFirstNameTextBox.Text.Trim();
                    string guardianMiddleName = GuardianMiddleNameTextBox.Text.Trim();
                    string guardianPhone = GuardianPhoneTextBox.Text.Trim();
                    string guardianEmail = GuardianEmailTextBox.Text.Trim();
                    Relationships selectedRelationship = _relationships.FirstOrDefault(r => r.RelationshipID == (int?)GuardianRelationshipComboBox.SelectedValue);

                    if (selectedRelationship != null)
                    {
                        var newGuardian = new Persons // Предполагается, что у вас есть таблица Persons для опекунов
                        {
                            Surname = guardianLastName,
                            Name = guardianFirstName,
                            MiddleName = guardianMiddleName,
                            PhoneNumber = guardianPhone,
                            Email = guardianEmail
                            // Могут быть и другие поля для опекуна
                        };
                        _dbContext.Persons.Add(newGuardian);
                        _dbContext.SaveChanges();

                        var newGuardianship = new Guardianships // Предполагается, что у вас есть таблица Guardianships для связи клиента и опекуна
                        {
                            ClientID = newClient.ClientID,
                            ResponsiblePersonID = newGuardian.PersonID, // Используем PersonID как идентификатор опекуна
                            RelationshipID = selectedRelationship.RelationshipID
                        };
                        _dbContext.Guardianships.Add(newGuardianship);
                    }
                }

                try
                {
                    _dbContext.SaveChanges();
                    MessageBox.Show($"Клиент {firstName} {lastName} успешно добавлен.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Очищаем поля после добавления
                    FirstNameTextBox.Clear();
                    LastNameTextBox.Clear();
                    MiddleNameTextBox.Clear();
                    PhoneNumberTextBox.Clear();
                    EmailTextBox.Clear();
                    DateOfBithTextBox.SelectedDate = null;
                    AddressTextBox.Text = "";
                    GenderComboBox.SelectedIndex = -1;
                    Notes.Clear();
                    ClientImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("/Resource/NewPerson.jpg", UriKind.Relative));
                    ClientImage.Tag = null;
                    StatusClientComboBox.SelectedIndex = 0;
                    NumberCardTextBox.Clear();
                    GuardianLastNameTextBox.Clear();
                    GuardianFirstNameTextBox.Clear();
                    GuardianMiddleNameTextBox.Clear();
                    GuardianPhoneTextBox.Clear();
                    GuardianEmailTextBox.Clear();
                    GuardianRelationshipComboBox.SelectedIndex = -1;
                    GuardianTabItem.Visibility = Visibility.Collapsed; // Скрываем вкладку опекуна после успешного добавления
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при добавлении клиента: {ex.Message}\n\n{ex.InnerException?.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return true;
            string pattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
            return Regex.IsMatch(email, pattern);
        }

        private bool IsValidPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
                return true;
            string pattern = @"^\+7\d{10}$";
            return Regex.IsMatch(phone, pattern);
        }

        private string GenerateQRCodeBase64(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return null;
            }

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(data, QRCodeGenerator.ECCLevel.Q);
            QRCode qrCode = new QRCode(qrCodeData);

            using (Bitmap qrCodeImage = qrCode.GetGraphic(20))
            {
                // Укажите папку для сохранения QR-кодов
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QRCodes");

                // Создайте папку, если она не существует
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                // Создайте уникальное имя файла (например, на основе номера карты)
                string fileName = $"QRCode_{data}.png";
                string filePath = Path.Combine(folderPath, fileName);

                try
                {
                    qrCodeImage.Save(filePath, ImageFormat.Png);
                    return filePath; // Возвращаем путь к сохраненному файлу
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении QR-кода: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }
            }
        }

        private void DateOfBithTextBox_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DateOfBithTextBox.SelectedDate.HasValue)
            {
                DateTime birthDate = DateOfBithTextBox.SelectedDate.Value;
                DateTime today = DateTime.Today;
                int age = today.Year - birthDate.Year;
                if (birthDate > today.AddYears(-age))
                    age--;
                GuardianTabItem.Visibility = age < 18 ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                GuardianTabItem.Visibility = Visibility.Collapsed;
            }
        }
    }
}