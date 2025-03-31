using FitnessCenterIS.Model;
using FitnessCenterIS.View.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FitnessCenterIS.View.Pages
{
    /// <summary>
    /// Interaction logic for ClientProfilePage.xaml
    /// </summary>
    public partial class ProfileClient : Page
    {
        private int _clientId;
        private Clients _currentClient;

        public ProfileClient(int clientId)
        {
            InitializeComponent();
            _clientId = clientId;
        }

        private void ClientProfilePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadClientData();
        }

        private void LoadClientData()
        {
            using (var context = new BDFitnessClubDipEntities())
            {
                _currentClient = context.Clients.FirstOrDefault(c => c.ClientID == _clientId);
                if (_currentClient != null)
                {
                    // Загрузка основной информации
                    ClientFullName.Text = $"{_currentClient.Persons.Surname} {_currentClient.Persons.Name} {_currentClient.Persons.MiddleName}";
                    ClientDateOfBirth.Text = _currentClient.Persons.DateOfBirth?.ToString("dd.MM.yyyy");
                    ClientGender.Text = _currentClient.Persons.Gender;
                    ClientEmail.Text = _currentClient.Persons.Email;
                    ClientPhoneNumber.Text = _currentClient.Persons.PhoneNumber;
                    ClientAddress.Text = _currentClient.Persons.Address;
                    ClientNotesTextBox.Text = string.IsNullOrEmpty(_currentClient.Persons.Notes)
                        ? "Заметок о клиенте нет"
                        : _currentClient.Persons.Notes;

                    // Установка цвета статуса клиента
                    SetStatusColor(_currentClient.StatusClient);

                    // Загрузка фотографии
                    LoadClientImage(_currentClient.Persons.ImagePerson);

                    // Загрузка QR-кода
                    LoadQRCode(_currentClient.QRCode);

                    // Загрузка дополнительной информации о клиенте
                    ClientIDTextBlock.Text = _currentClient.ClientID.ToString();
                    ClientCardNumber.Text = _currentClient.NumberCard;
                    ClientBonusBalance.Text = _currentClient.BonuseBalance.ToString();
                    ClientDepositBalance.Text = _currentClient.DepositBalance.ToString();
                    ClientStatus.Text = _currentClient.StatusClient;

                    // Загрузка уровня лояльности
                    var loyaltyLevel = context.LoyaltyLevels.FirstOrDefault(l => l.LoyaltyLevelID == _currentClient.LoyaltyLevelID);
                    ClientLoyaltyLevel.Text = loyaltyLevel?.Name ?? "Не указан";

                    // Загрузка опекунов
                    var guardians = context.Guardianships
                        .Where(g => g.ClientID == _currentClient.ClientID && g.ResponsiblePersonID.HasValue)
                        .Select(g => g.Persons.Surname + " " + g.Persons.Name + (string.IsNullOrEmpty(g.Persons.MiddleName) ? "" : " " + g.Persons.MiddleName))
                        .ToList();
                    ClientGuardiansList.ItemsSource = guardians.Count > 0 ? guardians : new List<string> { "Опекуны не назначены" };

                    // Загрузка абонементов
                    LoadSeasonTickets(context);

                    // Загрузка истории посещений
                    LoadVisitHistory(context);

                    // Загрузка задач
                    LoadTasks(context);
                }
                else
                {
                    MessageBox.Show($"Клиент с ID {_clientId} не найден.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    // Возврат на предыдущую страницу
                    NavigationService?.GoBack();
                }
            }
        }

        private void SetStatusColor(string status)
        {
            if (string.IsNullOrEmpty(status))
                return;

            switch (status.ToLower())
            {
                case "активен":
                    StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ecc71"));
                    break;
                case "заблокирован":
                    StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#e74c3c"));
                    break;
                case "приостановлен":
                    StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f39c12"));
                    break;
                default:
                    StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#95a5a6"));
                    break;
            }
        }

        private void LoadClientImage(string imagePath)
        {
            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    ClientImage.ImageSource = bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки изображения: {ex.Message}");
                    // Установка изображения по умолчанию
                    ClientImage.ImageSource = new BitmapImage(new Uri("/Resources/default_avatar.png", UriKind.Relative));
                }
            }
            else
            {
                // Установка изображения по умолчанию
                ClientImage.ImageSource = new BitmapImage(new Uri("/Resources/default_avatar.png", UriKind.Relative));
            }
        }

        private void LoadQRCode(string qrCodePath)
        {
            if (!string.IsNullOrEmpty(qrCodePath))
            {
                try
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(qrCodePath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    ClientQRCodeImage.Source = bitmap;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Ошибка загрузки QR-кода: {ex.Message}");
                    // Установка QR-кода по умолчанию или генерация нового
                    GenerateQRCode();
                }
            }
            else
            {
                // Генерация QR-кода, если он отсутствует
                GenerateQRCode();
            }
        }

        private void GenerateQRCode()
        {
            ClientQRCodeImage.Source = new BitmapImage(new Uri("/Resources/default_qrcode.png", UriKind.Relative));
        }

        private void LoadSeasonTickets(BDFitnessClubDipEntities context)
        {
            var seasonTickets = context.Sales
                .Where(s => s.Seasontickets != null &&
                            s.Seasontickets.SeasonticketClients.Any(sc => sc.ClientID == _currentClient.ClientID))
                .Select(s => new
                {
                    s.SaleID,
                    s.SaleDateTime,
                    s.RemainingVisits,
                    s.StatusSale,
                    s.PriceSold,
                    s.Seasontickets.Name,
                    s.StartDateTime,
                    s.EndDateTime
                })
                .ToList();

            ClientSeasonTicketsGrid.ItemsSource = seasonTickets;
        }



        private void LoadVisitHistory(BDFitnessClubDipEntities context)
        {
            var visits = context.Attendances
                .Where(v => v.ClientID == _currentClient.ClientID)
                .OrderByDescending(v => v.EntryDateTime)
                .Select(v => new
                {
                    v.EntryDateTime,
                    v.ExitDateTime,
                    KeyNumber = v.Lockers != null ? v.Lockers.KeyNumber : null,
                    DurationMinutes = v.ExitDateTime.HasValue && v.EntryDateTime.HasValue
                        ? System.Data.Entity.SqlServer.SqlFunctions.DateDiff("minute", v.EntryDateTime, v.ExitDateTime)
                        : null
                })
                .ToList()
                .Select(v => new
                {
                    v.EntryDateTime,
                    v.ExitDateTime,
                    v.KeyNumber,
                    Duration = v.DurationMinutes.HasValue
                        ? TimeSpan.FromMinutes(v.DurationMinutes.Value)
                        : (TimeSpan?)null,
                    FormattedDuration = v.DurationMinutes.HasValue
                        ? string.Format("{0:hh\\:mm}", TimeSpan.FromMinutes(v.DurationMinutes.Value))
                        : "Не завершено"
                })
                .ToList();

            ClientAttendancesGrid.ItemsSource = visits;
        }


        private void LoadTasks(BDFitnessClubDipEntities context)
        {
            var tasks = context.Tasks
                .Where(t => t.ClientID == _currentClient.ClientID)
                .Select(t => new
                {
                    t.TaskID,
                    t.Name,
                    t.Description,
                    t.StartDedlainDateTime,
                    t.EndDedlainDateTime,
                    t.Notes,
                    TaskPriority = context.TaskPriorities.FirstOrDefault(tp => tp.TaskPrioritieID == t.TaskPrioritieID),
                    TaskStatus = context.TaskStatuses.FirstOrDefault(ts => ts.TaskStatusID == t.TaskStatusID)
                })
                .ToList();

            ClientTasksGrid.ItemsSource = tasks;
        }

        private void AssignSeasonTicketButton_Click(object sender, RoutedEventArgs e)
        {
            // Открытие окна для добавления абонемента
            var addSeasonTicketWindow = new AddSeasonTicketWindow(_currentClient.ClientID);
            if (addSeasonTicketWindow.ShowDialog() == true)
            {
                // Перезагрузка данных после добавления абонемента
                LoadClientData();
            }
        }
        private void AssignServiceButton_Click(object sender, RoutedEventArgs e)
        {
            
        }

        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            // Создаем новый экземпляр окна задачи
            TaskWindow taskWindow = new TaskWindow();

            // Добавляем обработчик события TaskCreated
            taskWindow.TaskCreated += (s, args) =>
            {
                // Обновляем данные после создания задачи
                using (var context = new BDFitnessClubDipEntities())
                {
                    LoadTasks(context);
                }
            };

            // Предустанавливаем клиента в окне задачи
            // Предполагается, что у вас есть метод для установки клиента в TaskWindow
            taskWindow.SetClient(_clientId);

            // Показываем диалоговое окно
            taskWindow.ShowDialog();
            using (var context = new BDFitnessClubDipEntities())
            {
                LoadTasks(context);
            }
        }


        private void EditClientButton_Click(object sender, RoutedEventArgs e)
        {
            // Открытие окна для редактирования данных клиента
            var editClientWindow = new AddEditNewClientWindow(_currentClient.ClientID);
            if (editClientWindow.ShowDialog() == true)
            {
                // Перезагрузка данных после редактирования
                LoadClientData();
            }
        }

        private void ViewSeasonTicketDetails_Click(object sender, RoutedEventArgs e)
        {
            //// Получение выбранного абонемента
            //var button = sender as Button;
            //var seasonTicket = button.DataContext as dynamic;
            
            //if (seasonTicket != null)
            //{
            //    // Открытие окна с подробной информацией об абонементе
            //    var seasonTicketDetailsWindow = new SeasonTicketDetailsWindow(seasonTicket.SaleID);
            //    seasonTicketDetailsWindow.ShowDialog();
            //}
        }

        private void FreezeSeasonTicket_Click(object sender, RoutedEventArgs e)
        {
            //// Получение выбранного абонемента
            //var button = sender as Button;
            //var seasonTicket = button.DataContext as dynamic;
            
            //if (seasonTicket != null)
            //{
            //    // Проверка, можно ли заморозить абонемент
            //    if (seasonTicket.Status == "Активен")
            //    {
            //        // Открытие окна для заморозки абонемента
            //        var freezeSeasonTicketWindow = new FreezeSeasonTicketWindow(seasonTicket.SaleID);
            //        if (freezeSeasonTicketWindow.ShowDialog() == true)
            //        {
            //            // Перезагрузка данных после заморозки
            //            LoadClientData();
            //        }
            //    }
            //    else
            //    {
            //        MessageBox.Show("Заморозить можно только активный абонемент.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            //    }
            //}
        }

        private void ViewTaskDetails_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SaleButton_Click(object sender, RoutedEventArgs e)
        {
            NewSaleWindow saleWindow = new NewSaleWindow(_clientId);
            saleWindow.ShowDialog();

            // После закрытия окна обновляем данные профиля, в частности абонементы
            using (var context = new BDFitnessClubDipEntities())
            {
                LoadSeasonTickets(context);
                // Обновляем бонусный баланс
                _currentClient = context.Clients.FirstOrDefault(c => c.ClientID == _clientId);
                if (_currentClient != null)
                {
                    ClientBonusBalance.Text = _currentClient.BonuseBalance.ToString();
                    ClientDepositBalance.Text = _currentClient.DepositBalance.ToString();
                }
            }
        }
    }

    // Вспомогательные классы для создания окон, которые будут использоваться в коде выше

    /// <summary>
    /// Окно для добавления абонемента клиенту
    /// </summary>
    public class AddSeasonTicketWindow : Window
    {
        private int _clientId;

        public AddSeasonTicketWindow(int clientId)
        {
            _clientId = clientId;
            Title = "Добавление абонемента";
            Width = 500;
            Height = 400;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            
            // Здесь должно быть создание интерфейса окна
            // Для примера просто создадим заглушку
            
            var grid = new Grid();
            Content = grid;
            
            var textBlock = new TextBlock
            {
                Text = "Форма добавления абонемента",
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            grid.Children.Add(textBlock);
            
            // Добавление кнопок OK и Отмена
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 20)
            };
            
            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0)
            };
            okButton.Click += (s, e) => { DialogResult = true; };
            
            var cancelButton = new Button
            {
                Content = "Отмена",
                Width = 80,
                Height = 30
            };
            cancelButton.Click += (s, e) => { DialogResult = false; };
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            
            grid.Children.Add(buttonPanel);
        }
    }
}
