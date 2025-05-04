using FitnessCenterIS.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using System.Threading;
using System.Text;

namespace FitnessCenterIS.View.Windows
{
    public partial class SendScheduleWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private readonly string _schedulePeriod;
        private readonly FrameworkElement _scheduleElement;
        private List<EmailRecipient> _availableRecipients;
        private CancellationTokenSource _cancellationTokenSource;

        // Настройки SMTP сервера (можно вынести в конфигурацию)
        private const string SmtpServer = "smtp.mail.ru"; // Замените на реальный SMTP сервер
        private const int SmtpPort = 587;
        private const string SmtpUsername = "fitness.clublive@mail.ru"; // Замените на реальный email
        private const string SmtpPassword = "0iHFGPSQk2mqQGyejNCb"; // Замените на реальный пароль
        private const bool EnableSsl = true;

        public SendScheduleWindow(BDFitnessClubDipEntities dbContext, string schedulePeriod, FrameworkElement scheduleElement)
        {
            InitializeComponent(); // Сначала обязательно вызываем InitializeComponent

            _dbContext = dbContext;
            _schedulePeriod = schedulePeriod;
            _scheduleElement = scheduleElement;
            //_smtpSettings = SmtpConfiguration.GetSmtpSettings();
            SchedulePeriodTextBlock.Text = schedulePeriod;

            LoadRecipients();

            // Вызываем LoadRecipients() и после этого вызываем Loaded event
            this.Loaded += SendScheduleWindow_Loaded;
        }

        private void SendScheduleWindow_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateRecipientVisibility();
        }

        private void LoadRecipients()
        {
            _availableRecipients = new List<EmailRecipient>();

            try
            {
                // Добавляем опцию "Все группы"
                _availableRecipients.Add(new EmailRecipient
                {
                    Id = -1,
                    Name = "Все группы (кроме индивидуальных)",
                    Type = EmailRecipientType.AllGroups
                });

                // Исправленная загрузка групп
                var groups = _dbContext.Groups
                    .Where(g => g.StatusActivity == "Активно" || g.StatusActivity == "Активна")
                    .ToList();

                //// Выводим отладочную информацию
                //MessageBox.Show($"Найдено групп: {groups.Count}", "Отладка", MessageBoxButton.OK, MessageBoxImage.Information);

                foreach (var group in groups)
                {
                    _availableRecipients.Add(new EmailRecipient
                    {
                        Id = group.GroupID,
                        Name = group.Name,
                        Type = EmailRecipientType.Group,
                        SourceId = group.GroupID
                    });
                }

                // Загружаем клиентов с email
                var clients = _dbContext.Clients
                    .Where(c => c.PersonID != null)
                    .Select(c => new
                    {
                        ClientID = c.ClientID,
                        Email = c.Persons.Email,
                        Surname = c.Persons.Surname,
                        Name = c.Persons.Name,
                        MiddleName = c.Persons.MiddleName
                    })
                    .Where(c => !string.IsNullOrEmpty(c.Email))
                    .ToList();

                //// Выводим отладочную информацию
                //MessageBox.Show($"Найдено клиентов: {clients.Count}", "Отладка", MessageBoxButton.OK, MessageBoxImage.Information);

                foreach (var client in clients)
                {
                    _availableRecipients.Add(new EmailRecipient
                    {
                        Id = client.ClientID,
                        Name = $"{client.Surname} {client.Name} {client.MiddleName}".Trim(),
                        Email = client.Email,
                        Type = EmailRecipientType.Individual,
                        SourceId = client.ClientID
                    });
                }

                //// Выводим итоговое количество получателей
                //MessageBox.Show($"Всего получателей: {_availableRecipients.Count}", "Отладка", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке получателей: {ex.Message}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateRecipientVisibility()
        {
            if (RecipientLabel == null || RecipientComboBox == null || SendTypeComboBox == null)
                return;

            ComboBoxItem selectedItem = SendTypeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                switch (selectedItem.Content.ToString())
                {
                    case "Всем группам (кроме индивидуальных)":
                        RecipientLabel.Visibility = Visibility.Collapsed;
                        RecipientComboBox.Visibility = Visibility.Collapsed;
                        break;

                    case "Конкретной группе":
                        RecipientLabel.Visibility = Visibility.Visible;
                        RecipientComboBox.Visibility = Visibility.Visible;
                        RecipientLabel.Text = "Выберите группу";

                        // Отладка: проверяем доступных получателей
                        var groupRecipients = _availableRecipients
                            .Where(r => r.Type == EmailRecipientType.Group)
                            .ToList();

                        MessageBox.Show($"Групп в списке: {groupRecipients.Count}", "Отладка", MessageBoxButton.OK, MessageBoxImage.Information);

                        if (groupRecipients.Count > 0)
                        {
                            foreach (var group in groupRecipients)
                            {
                                MessageBox.Show($"Группа: {group.Name}", "Отладка", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }

                        RecipientComboBox.ItemsSource = groupRecipients;
                        break;

                    case "Конкретному клиенту":
                        RecipientLabel.Visibility = Visibility.Visible;
                        RecipientComboBox.Visibility = Visibility.Visible;
                        RecipientLabel.Text = "Выберите клиента";

                        var clientRecipients = _availableRecipients
                            .Where(r => r.Type == EmailRecipientType.Individual)
                            .ToList();

                        RecipientComboBox.ItemsSource = clientRecipients;
                        break;
                }
            }
        }

        private async void SendButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Отключаем кнопку отправки
                SendButton.IsEnabled = false;
                ProgressBar.Visibility = Visibility.Visible;
                StatusTextBlock.Visibility = Visibility.Visible;
                StatusTextBlock.Text = "Подготовка к отправке...";

                // Создаем снимок расписания
                BitmapSource scheduleImage = CaptureSchedule();
                if (scheduleImage == null)
                {
                    throw new Exception("Не удалось создать изображение расписания.");
                }

                // Определяем получателей
                List<EmailRecipient> recipients = GetRecipients();

                MessageBox.Show($"Найдено получателей для отправки: {recipients.Count}", "Отладка",
                                MessageBoxButton.OK, MessageBoxImage.Information);

                if (recipients.Count == 0)
                {
                    throw new Exception("Не найдено получателей для отправки.");
                }

                // Создаем токен отмены
                _cancellationTokenSource = new CancellationTokenSource();

                // Отправляем письма
                await SendEmailsAsync(recipients, scheduleImage, _cancellationTokenSource.Token);

                StatusTextBlock.Text = $"Письма успешно отправлены {recipients.Count} получателям.";
                StatusTextBlock.Foreground = Brushes.Green;

                // Автоматически закрываем окно через 2 секунды
                await Task.Delay(2000);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Ошибка при отправке: {ex.Message}";
                StatusTextBlock.Foreground = Brushes.Red;
            }
        }

        private BitmapSource CaptureSchedule()
        {
            try
            {
                // Получаем размеры элемента
                double width = _scheduleElement.ActualWidth;
                double height = _scheduleElement.ActualHeight;

                if (width <= 0 || height <= 0)
                {
                    // Принудительно измеряем элемент
                    _scheduleElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    _scheduleElement.Arrange(new Rect(0, 0, _scheduleElement.DesiredSize.Width, _scheduleElement.DesiredSize.Height));

                    width = _scheduleElement.ActualWidth;
                    height = _scheduleElement.ActualHeight;
                }

                if (width <= 0 || height <= 0)
                {
                    // Используем значения по умолчанию, если размер все еще нулевой
                    width = 1200;
                    height = 800;
                }

                // Создаем визуальный снимок
                RenderTargetBitmap renderTarget = new RenderTargetBitmap(
                    (int)width,
                    (int)height,
                    96,
                    96,
                    PixelFormats.Pbgra32);

                // Рендерим элемент
                renderTarget.Render(_scheduleElement);

                return renderTarget;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании снимка расписания: {ex.Message}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        private List<EmailRecipient> GetRecipients()
        {
            ComboBoxItem selectedType = SendTypeComboBox.SelectedItem as ComboBoxItem;
            if (selectedType == null) return new List<EmailRecipient>();

            switch (selectedType.Content.ToString())
            {
                case "Всем группам (кроме индивидуальных)":
                    return GetAllGroupRecipients();

                case "Конкретной группе":
                    EmailRecipient selectedGroup = RecipientComboBox.SelectedItem as EmailRecipient;
                    if (selectedGroup != null)
                    {
                        return GetGroupClients(selectedGroup.SourceId.Value);
                    }
                    return new List<EmailRecipient>();

                case "Конкретному клиенту":
                    EmailRecipient selectedClient = RecipientComboBox.SelectedItem as EmailRecipient;
                    if (selectedClient != null)
                    {
                        return new List<EmailRecipient> { selectedClient };
                    }
                    return new List<EmailRecipient>();

                default:
                    return new List<EmailRecipient>();
            }
        }

        private List<EmailRecipient> GetAllGroupRecipients()
        {
            List<EmailRecipient> recipients = new List<EmailRecipient>();

            try
            {
                // Получаем всех клиентов групповых занятий через SeasonticketServices
                var groupClientsData = _dbContext.SeasonticketServices
                    .Where(ss => ss.AccessAllowed == true)
                    .SelectMany(ss => _dbContext.GroupMembers
                        .Where(gm => gm.SeasonticketServiceID == ss.SeasonticketServiceID)
                        .SelectMany(gm => _dbContext.Groups
                            .Where(g => g.GroupID == gm.GroupID)
                            .SelectMany(g => _dbContext.Clients
                                .Where(c => c.PersonID != null && !string.IsNullOrEmpty(c.Persons.Email))
                                .Select(c => new
                                {
                                    ClientID = c.ClientID,
                                    Email = c.Persons.Email,
                                    Surname = c.Persons.Surname,
                                    Name = c.Persons.Name,
                                    MiddleName = c.Persons.MiddleName
                                }))))
                    .Distinct()
                    .ToList();

                foreach (var client in groupClientsData)
                {
                    recipients.Add(new EmailRecipient
                    {
                        Id = client.ClientID,
                        Name = $"{client.Surname} {client.Name} {client.MiddleName}".Trim(),
                        Email = client.Email,
                        Type = EmailRecipientType.Individual,
                        SourceId = client.ClientID
                    });
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Ошибка при получении получателей: {ex.Message}";
                StatusTextBlock.Foreground = Brushes.Red;
                StatusTextBlock.Visibility = Visibility.Visible;
            }

            return recipients;
        }

        private List<EmailRecipient> GetGroupClients(int groupId)
        {
            List<EmailRecipient> recipients = new List<EmailRecipient>();

            try
            {
                // Метод 1: Через SeasonticketClients и Sales
                var clientsInGroup = (from gm in _dbContext.GroupMembers
                                      where gm.GroupID == groupId
                                      join ss in _dbContext.SeasonticketServices on gm.SeasonticketServiceID equals ss.SeasonticketServiceID
                                      join sale in _dbContext.Sales on ss.SaleID equals sale.SaleID
                                      join sc in _dbContext.SeasonticketClients on sale.SeasonticketID equals sc.SeasonticketID
                                      join c in _dbContext.Clients on sc.ClientID equals c.ClientID
                                      join p in _dbContext.Persons on c.PersonID equals p.PersonID
                                      where !string.IsNullOrEmpty(p.Email)
                                      select new
                                      {
                                          ClientID = c.ClientID,
                                          Email = p.Email,
                                          Surname = p.Surname,
                                          Name = p.Name,
                                          MiddleName = p.MiddleName
                                      }).Distinct().ToList();

                // Если метод 1 не дал результатов, пробуем метод 2: поиск клиентов через расписание
                if (clientsInGroup.Count == 0)
                {
                    clientsInGroup = (from sch in _dbContext.Schedules
                                      where sch.GroupID == groupId && sch.ClientID != null
                                      join c in _dbContext.Clients on sch.ClientID equals c.ClientID
                                      join p in _dbContext.Persons on c.PersonID equals p.PersonID
                                      where !string.IsNullOrEmpty(p.Email)
                                      select new
                                      {
                                          ClientID = c.ClientID,
                                          Email = p.Email,
                                          Surname = p.Surname,
                                          Name = p.Name,
                                          MiddleName = p.MiddleName
                                      }).Distinct().ToList();
                }

                foreach (var client in clientsInGroup)
                {
                    recipients.Add(new EmailRecipient
                    {
                        Id = client.ClientID,
                        Name = $"{client.Surname} {client.Name} {client.MiddleName}".Trim(),
                        Email = client.Email,
                        Type = EmailRecipientType.Individual,
                        SourceId = client.ClientID
                    });
                }

                MessageBox.Show($"Найдено получателей для группы {groupId}: {recipients.Count}", "Отладка",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при получении получателей группы: {ex.Message}\n{ex.StackTrace}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return recipients;
        }


        private async Task SendEmailsAsync(List<EmailRecipient> recipients, BitmapSource scheduleImage, CancellationToken cancellationToken)
        {
            int totalEmails = recipients.Count;
            int sentEmails = 0;

            using (SmtpClient smtpClient = new SmtpClient(SmtpServer, SmtpPort))
            {
                smtpClient.Credentials = new NetworkCredential(SmtpUsername, SmtpPassword);
                smtpClient.EnableSsl = EnableSsl;
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;

                foreach (var recipient in recipients)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        StatusTextBlock.Text = "Отправка отменена пользователем.";
                        StatusTextBlock.Foreground = Brushes.Orange;
                        return;
                    }

                    try
                    {
                        using (MailMessage mail = new MailMessage())
                        {
                            mail.From = new MailAddress(SmtpUsername);
                            mail.To.Add(recipient.Email);
                            mail.Subject = SubjectTextBox.Text;
                            mail.Body = BodyTextBox.Text;

                            // Добавляем изображение как вложение
                            byte[] imageBytes = ImageToByteArray(scheduleImage);
                            MemoryStream imageStream = new MemoryStream(imageBytes);
                            Attachment attachment = new Attachment(imageStream, "schedule.png", "image/png");
                            mail.Attachments.Add(attachment);

                            // Отправляем письмо асинхронно
                            await smtpClient.SendMailAsync(mail);

                            sentEmails++;

                            // Обновляем статус
                            StatusTextBlock.Text = $"Отправлено {sentEmails} из {totalEmails} писем...";

                            // Небольшая задержка между отправками
                            await Task.Delay(500, cancellationToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Логируем ошибку, но продолжаем отправку другим получателям
                        Console.WriteLine($"Ошибка при отправке письма {recipient.Email}: {ex.Message}");
                    }
                }
            }
        }

        private byte[] ImageToByteArray(BitmapSource image)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(stream);
                return stream.ToArray();
            }
        }

        private void SendTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateRecipientVisibility();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
            }

            DialogResult = false;
            Close();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}