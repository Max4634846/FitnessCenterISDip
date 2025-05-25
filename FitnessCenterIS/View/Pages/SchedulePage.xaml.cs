using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Documents;
using System.IO;
using System.Text;
using FitnessCenterIS.View.Windows;
using FitnessCenterIS.Model;
using System.Diagnostics;
using System.Windows.Markup;
using System.Windows.Media.Imaging;

namespace FitnessCenterIS.View.Pages
{
    public partial class SchedulePage : Page
    {
        private DateTime _currentDate = DateTime.Today;
        private ViewMode _currentViewMode = ViewMode.Week;
        private List<Schedules> _scheduleItems;
        private BDFitnessClubDipEntities _dbContext;
        private Dictionary<int, string> _scheduleColors = new Dictionary<int, string>();

        private List<ScheduleItem> _scheduleItemsWrapper = new List<ScheduleItem>();

        public enum ViewMode
        {
            Day,
            Week,
            Month
        }

        public SchedulePage()
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            _scheduleColors = new Dictionary<int, string>();
            InitializeScheduleColors();
            LoadScheduleItems();
            UpdateDateRangeText();
            GenerateScheduleView();
            this.Loaded += SchedulePage_Loaded;
        }

        public SchedulePage(ViewMode initialViewMode)
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            _currentViewMode = initialViewMode;
            _scheduleColors = new Dictionary<int, string>();
            InitializeScheduleColors();
            LoadScheduleItems();
            UpdateDateRangeText();
            GenerateScheduleView();
            this.Loaded += SchedulePage_Loaded;
        }


        private void SchedulePage_Loaded(object sender, RoutedEventArgs e)
        {
            // Установка правильного выбора в ComboBox в соответствии с текущим режимом
            switch (_currentViewMode)
            {
                case ViewMode.Day:
                    ViewModeComboBox.SelectedIndex = 0;
                    break;
                case ViewMode.Week:
                    ViewModeComboBox.SelectedIndex = 1;
                    break;
                case ViewMode.Month:
                    ViewModeComboBox.SelectedIndex = 2;
                    break;
            }

            GenerateScheduleView();
        }

        private void LoadScheduleItems()
        {
            try
            {
                // Загружаем все занятия из базы данных с нужными связями
                var allScheduleItems = _dbContext.Schedules
                    .Include("Rooms")
                    .Include("Staffs.Persons")
                    .Include("Clients.Persons")
                    .Include("Groups")
                    .ToList();

                // Убираем фильтр по статусу, чтобы видеть все записи, или явно укажите нужные статусы
                _scheduleItems = allScheduleItems
                    //.Where(item => item.ScheduleStatus == null || item.ScheduleStatus == "Активно")
                    .ToList();

                _scheduleItemsWrapper.Clear();

                foreach (var item in _scheduleItems)
                {
                    // Определяем цвет для расписания
                    string color = _scheduleColors.ContainsKey(item.ScheduleID)
                        ? _scheduleColors[item.ScheduleID]
                        : GetColorForSchedule(item);

                    // Создаем обертку ScheduleItem
                    _scheduleItemsWrapper.Add(new ScheduleItem(item, color));

                    // Сохраняем цвет для будущего использования
                    _scheduleColors[item.ScheduleID] = color;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке расписания: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Метод для генерации случайного цвета
        private string GetRandomColor(Random random)
        {
            // Массив предопределенных цветов
            string[] predefinedColors = new string[]
            {
                "#FF5C5C", // Красный
                "#2196F3", // Синий
                "#FF9E00", // Оранжевый
                "#4CAF50", // Зеленый
                "#9C27B0", // Фиолетовый
                "#00BCD4", // Бирюзовый
                "#607D8B", // Серый
                "#3498db"  // Голубой
            };

            // Выбираем случайный цвет из массива
            return predefinedColors[random.Next(predefinedColors.Length)];
        }



        public string GetColorForSchedule(Schedules schedule)
        {
            // Проверяем, есть ли уже цвет для этого расписания
            if (schedule.ScheduleID > 0 && _scheduleColors.ContainsKey(schedule.ScheduleID))
            {
                return _scheduleColors[schedule.ScheduleID];
            }

            // Определяем цвет в зависимости от типа услуги или другого критерия
            if (schedule.SeasonticketServiceID.HasValue)
            {
                var service = _dbContext.Services
                    .FirstOrDefault(s => s.ServiceID == schedule.SeasonticketServiceID.Value);

                if (service != null && service.ServiceClassificationID.HasValue)
                {
                    // Определяем цвет на основе классификации услуги
                    switch (service.ServiceClassificationID.Value % 6)
                    {
                        case 0: return "#FF5C5C"; // Красный
                        case 1: return "#2196F3"; // Синий
                        case 2: return "#FF9E00"; // Оранжевый
                        case 3: return "#4CAF50"; // Зеленый
                        case 4: return "#9C27B0"; // Фиолетовый
                        case 5: return "#00BCD4"; // Бирюзовый
                        default: return "#607D8B"; // Серый
                    }
                }
                else
                {
                    // Если классификация не определена, используем ID услуги
                    int serviceId = schedule.SeasonticketServiceID.Value;

                    switch (serviceId % 6)
                    {
                        case 0: return "#FF5C5C"; // Красный
                        case 1: return "#2196F3"; // Синий
                        case 2: return "#FF9E00"; // Оранжевый
                        case 3: return "#4CAF50"; // Зеленый
                        case 4: return "#9C27B0"; // Фиолетовый
                        case 5: return "#00BCD4"; // Бирюзовый
                        default: return "#607D8B"; // Серый
                    }
                }
            }

            // Цвет по умолчанию, если не удалось определить на основе услуги
            return "#3498db";
        }

        private void UpdateDateRangeText()
        {
            if (DateRangeTextBlock == null) return;

            switch (_currentViewMode)
            {
                case ViewMode.Day:
                    DateRangeTextBlock.Text = _currentDate.ToString("dd MMMM yyyy");
                    break;
                case ViewMode.Week:
                    DateTime weekStart = GetStartOfWeek(_currentDate);
                    DateTime weekEnd = weekStart.AddDays(6);
                    DateRangeTextBlock.Text = $"{weekStart:dd.MM} - {weekEnd:dd.MM.yyyy}";
                    break;
                case ViewMode.Month:
                    DateRangeTextBlock.Text = _currentDate.ToString("MMMM yyyy");
                    break;
            }
        }

        private DateTime GetStartOfWeek(DateTime date)
        {
            int diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-1 * diff).Date;
        }

        private void GenerateScheduleView()
        {
            if (ScheduleGrid == null)
            {
                return;
            }

            ScheduleGrid.Children.Clear();
            ScheduleGrid.RowDefinitions.Clear();
            ScheduleGrid.ColumnDefinitions.Clear();

            switch (_currentViewMode)
            {
                case ViewMode.Day:
                    GenerateDayView();
                    break;
                case ViewMode.Week:
                    GenerateWeekView();
                    break;
                case ViewMode.Month:
                    GenerateMonthView();
                    break;
            }
        }

        private void AddScheduleItemToGrid(Schedules item, int column)
        {
            // Найдем объект ScheduleItem для текущего занятия
            var scheduleItem = _scheduleItemsWrapper.FirstOrDefault(si => si.Schedule.ScheduleID == item.ScheduleID);
            string scheduleColor = scheduleItem?.Color ?? "#607D8B"; // Серый по умолчанию

            // Получаем время начала и окончания
            int startHour = item.StartDateTime?.Hour ?? 0;
            int startMinute = item.StartDateTime?.Minute ?? 0;
            int endHour = item.EndDateTime?.Hour ?? 0;
            int endMinute = item.EndDateTime?.Minute ?? 0;

            // Вычисляем строку начала (с учетом того, что мы начинаем с 8:00)
            double startRow = (startHour - 8) + (startMinute / 60.0);
            if (startRow < 0) startRow = 0; // Если занятие начинается раньше 8:00

            // Вычисляем продолжительность в часах
            double duration = (endHour - startHour) + ((endMinute - startMinute) / 60.0);

            // Устанавливаем минимальную высоту блока в 1 час
            duration = Math.Max(duration, 1.0);

            if (startRow + duration > 13) duration = 13 - startRow; // Ограничиваем до 20:00

            // Создаем контейнер для элемента расписания в стиле карточки
            Grid eventContainer = new Grid();
            eventContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            eventContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Цветная полоса слева
            Border colorStrip = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheduleColor)),
                CornerRadius = new CornerRadius(5, 0, 0, 5)
            };
            Grid.SetColumn(colorStrip, 0);
            Grid.SetRowSpan(colorStrip, 1);
            eventContainer.Children.Add(colorStrip);

            // Создаем содержимое элемента (белая карточка)
            Border contentBorder = new Border
            {
                Background = Brushes.White,
                BorderThickness = new Thickness(0, 1, 1, 1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                CornerRadius = new CornerRadius(0, 5, 5, 0),
                Padding = new Thickness(8)
            };
            Grid.SetColumn(contentBorder, 1);

            // Создаем StackPanel для текстового содержимого
            StackPanel content = new StackPanel();

            // Заголовок занятия
            TextBlock titleText = new TextBlock
            {
                Text = item.Title ?? "",
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = Brushes.Black,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 2)
            };
            content.Children.Add(titleText);

            // Время проведения
            if (scheduleItem?.StartDateTime != null && scheduleItem?.EndDateTime != null)
            {
                TextBlock timeText = new TextBlock
                {
                    Text = $"{scheduleItem.StartDateTime:HH:mm} - {scheduleItem.EndDateTime:HH:mm}",
                    FontSize = 11,
                    Foreground = Brushes.Black,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                content.Children.Add(timeText);
            }

            // Место проведения
            if (!string.IsNullOrEmpty(scheduleItem?.RoomName))
            {
                TextBlock locationText = new TextBlock
                {
                    Text = scheduleItem.RoomName,
                    FontSize = 11,
                    Foreground = Brushes.Black,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                content.Children.Add(locationText);
            }

            // Тренер
            if (!string.IsNullOrEmpty(scheduleItem?.TrainerName))
            {
                TextBlock trainerText = new TextBlock
                {
                    Text = scheduleItem.TrainerName,
                    FontSize = 11,
                    Foreground = Brushes.Black,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                content.Children.Add(trainerText);
            }

            // Добавляем информацию о клиенте, если он есть
            if (!string.IsNullOrEmpty(scheduleItem?.ClientName))
            {
                TextBlock clientText = new TextBlock
                {
                    Text = $"Клиент: {scheduleItem.ClientName}",
                    FontSize = 11,
                    Foreground = Brushes.Black,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                content.Children.Add(clientText);
            }

            // Добавляем информацию о группе, если она есть
            if (!string.IsNullOrEmpty(scheduleItem?.GroupName))
            {
                TextBlock groupText = new TextBlock
                {
                    Text = $"Группа: {scheduleItem.GroupName}",
                    FontSize = 11,
                    Foreground = Brushes.Black,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                content.Children.Add(groupText);
            }

            contentBorder.Child = content;
            eventContainer.Children.Add(contentBorder);

            // Добавляем эффект тени
            eventContainer.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                ShadowDepth = 1,
                BlurRadius = 4,
                Opacity = 0.2,
                Color = Colors.Black
            };

            // Устанавливаем позицию в сетке
            Grid.SetRow(eventContainer, (int)startRow + 1); // +1 для заголовка
            Grid.SetColumn(eventContainer, column);
            Grid.SetRowSpan(eventContainer, (int)Math.Ceiling(duration));

            // Добавляем отступы
            eventContainer.Margin = new Thickness(4);

            // Добавляем обработчик события для редактирования
            eventContainer.MouseLeftButtonDown += (s, e) => EditScheduleItem(item);

            // Добавляем элемент в сетку
            ScheduleGrid.Children.Add(eventContainer);
        }


        private void GenerateDayView()
        {
            // Очистка сетки
            ScheduleGrid.Children.Clear();
            ScheduleGrid.RowDefinitions.Clear();
            ScheduleGrid.ColumnDefinitions.Clear();

            // Настройка сетки
            ScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) }); // Заголовок дня

            // Добавляем строки для каждого часа (с 8:00 до 20:00)
            for (int i = 8; i <= 20; i++)
            {
                ScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            }

            // Колонка для времени
            ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            // Колонка для занятий текущего дня
            ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Добавляем заголовок дня
            Border dayHeaderBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                Padding = new Thickness(8)
            };

            TextBlock dayHeaderText = new TextBlock
            {
                Text = _currentDate.ToString("dddd, dd MMMM"),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            dayHeaderBorder.Child = dayHeaderText;
            Grid.SetRow(dayHeaderBorder, 0);
            Grid.SetColumn(dayHeaderBorder, 1);
            ScheduleGrid.Children.Add(dayHeaderBorder);

            // Добавляем метки времени и горизонтальные линии
            for (int i = 8; i <= 20; i++)
            {
                // Метка времени
                Border timeSlotBorder = new Border
                {
                    BorderThickness = new Thickness(0, 0, 1, 0),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                    Padding = new Thickness(10),
                    Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
                };

                TextBlock timeLabel = new TextBlock
                {
                    Text = $"{i:00}:00",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
                };

                timeSlotBorder.Child = timeLabel;
                Grid.SetRow(timeSlotBorder, i - 7); // -7 потому что начинаем с 8:00
                Grid.SetColumn(timeSlotBorder, 0);
                ScheduleGrid.Children.Add(timeSlotBorder);

                // Горизонтальная линия для часа
                Border hourSlotBorder = new Border
                {
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                    Background = (i % 2 == 0) ?
                        new SolidColorBrush(Color.FromRgb(250, 250, 250)) :
                        Brushes.Transparent
                };

                Grid.SetRow(hourSlotBorder, i - 7);
                Grid.SetColumn(hourSlotBorder, 1);
                ScheduleGrid.Children.Add(hourSlotBorder);
            }

            // Отображаем занятия для выбранного дня
            var dayItems = _scheduleItems.Where(item =>
                item.StartDateTime?.Date == _currentDate.Date).ToList();

            foreach (var item in dayItems)
            {
                AddScheduleItemToGrid(item, 1);
            }
        }

        private void GenerateWeekView()
        {
            // Очистка сетки
            ScheduleGrid.Children.Clear();
            ScheduleGrid.RowDefinitions.Clear();
            ScheduleGrid.ColumnDefinitions.Clear();

            // Настройка сетки
            ScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) }); // Заголовок дней

            // Добавляем строки для каждого часа (с 8:00 до 20:00)
            for (int i = 8; i <= 20; i++)
            {
                ScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) });
            }

            // Колонка для времени - фиксированной ширины
            ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });

            // Добавляем колонки для каждого дня недели - равной ширины
            for (int i = 0; i < 7; i++)
            {
                ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Получаем начало недели
            DateTime weekStart = GetStartOfWeek(_currentDate);

            // Добавляем заголовки дней недели
            for (int i = 0; i < 7; i++)
            {
                DateTime day = weekStart.AddDays(i);

                Border dayHeaderBorder = new Border
                {
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                    Padding = new Thickness(5),
                    Background = day.Date == DateTime.Today ?
                        new SolidColorBrush(Color.FromRgb(240, 247, 255)) :
                        Brushes.Transparent
                };

                StackPanel headerPanel = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                TextBlock dayOfWeekText = new TextBlock
                {
                    Text = day.ToString("ddd"),
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(117, 117, 117)),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                TextBlock dayNumberText = new TextBlock
                {
                    Text = day.Day.ToString(),
                    FontSize = 16,
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 2, 0, 0),
                    Foreground = day.Date == DateTime.Today ?
                        new SolidColorBrush(Color.FromRgb(33, 150, 243)) :
                        new SolidColorBrush(Color.FromRgb(51, 51, 51))
                };

                headerPanel.Children.Add(dayOfWeekText);
                headerPanel.Children.Add(dayNumberText);

                dayHeaderBorder.Child = headerPanel;
                Grid.SetRow(dayHeaderBorder, 0);
                Grid.SetColumn(dayHeaderBorder, i + 1);
                ScheduleGrid.Children.Add(dayHeaderBorder);
            }

            // Добавляем метки времени и горизонтальные линии
            for (int i = 8; i <= 20; i++)
            {
                // Метка времени
                Border timeSlotBorder = new Border
                {
                    BorderThickness = new Thickness(0, 0, 1, 0),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                    Padding = new Thickness(10),
                    Background = new SolidColorBrush(Color.FromRgb(250, 250, 250))
                };

                TextBlock timeLabel = new TextBlock
                {
                    Text = $"{i:00}:00",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
                };

                timeSlotBorder.Child = timeLabel;
                Grid.SetRow(timeSlotBorder, i - 7); // -7 потому что начинаем с 8:00
                Grid.SetColumn(timeSlotBorder, 0);
                ScheduleGrid.Children.Add(timeSlotBorder);

                // Горизонтальные линии для каждого часа
                for (int j = 1; j <= 7; j++)
                {
                    Border hourSlotBorder = new Border
                    {
                        BorderThickness = new Thickness(0, 1, 0, 0),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                        Background = (i % 2 == 0) ?
                            new SolidColorBrush(Color.FromRgb(252, 252, 252)) :
                            Brushes.Transparent
                    };

                    Grid.SetRow(hourSlotBorder, i - 7);
                    Grid.SetColumn(hourSlotBorder, j);
                    ScheduleGrid.Children.Add(hourSlotBorder);
                }
            }

            // Отображаем занятия для выбранной недели
            for (int i = 0; i < 7; i++)
            {
                DateTime day = weekStart.AddDays(i);
                var dayItems = _scheduleItems.Where(item =>
                    item.StartDateTime?.Date == day.Date).ToList();

                foreach (var item in dayItems)
                {
                    AddScheduleItemToGrid(item, i + 1);
                }
            }
        }

        private void GenerateMonthView()
        {
            // Очистка сетки
            ScheduleGrid.Children.Clear();
            ScheduleGrid.RowDefinitions.Clear();
            ScheduleGrid.ColumnDefinitions.Clear();

            // Получаем первый день месяца
            DateTime firstDayOfMonth = new DateTime(_currentDate.Year, _currentDate.Month, 1);
            // Получаем количество дней в месяце
            int daysInMonth = DateTime.DaysInMonth(_currentDate.Year, _currentDate.Month);
            // Получаем день недели для первого дня месяца (0 = воскресенье, 1 = понедельник, и т.д.)
            int firstDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
            // Корректируем, чтобы понедельник был первым днем недели
            if (firstDayOfWeek == 0) firstDayOfWeek = 7;
            firstDayOfWeek--;

            // Настройка сетки
            // Заголовок месяца
            ScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            // Заголовки дней недели
            ScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            // Строки для недель (максимум 6 недель в месяце)
            for (int i = 0; i < 6; i++)
            {
                ScheduleGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });
            }

            // Колонки для дней недели (7 дней)
            for (int i = 0; i < 7; i++)
            {
                ScheduleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Добавляем заголовок месяца
            TextBlock monthHeader = new TextBlock
            {
                Text = _currentDate.ToString("MMMM yyyy"),
                FontWeight = FontWeights.Bold,
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(monthHeader, 0);
            Grid.SetColumnSpan(monthHeader, 7);
            ScheduleGrid.Children.Add(monthHeader);

            // Добавляем заголовки дней недели
            string[] dayNames = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };
            for (int i = 0; i < 7; i++)
            {
                Border dayNameBorder = new Border
                {
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                    Padding = new Thickness(4)
                };

                TextBlock dayHeader = new TextBlock
                {
                    Text = dayNames[i],
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(117, 117, 117))
                };

                dayNameBorder.Child = dayHeader;
                Grid.SetRow(dayNameBorder, 1);
                Grid.SetColumn(dayNameBorder, i);
                ScheduleGrid.Children.Add(dayNameBorder);
            }

            // Добавляем дни месяца
            int day = 1;
            for (int week = 0; week < 6; week++)
            {
                for (int dayOfWeek = 0; dayOfWeek < 7; dayOfWeek++)
                {
                    // Пропускаем ячейки до первого дня месяца
                    if (week == 0 && dayOfWeek < firstDayOfWeek)
                    {
                        continue;
                    }

                    // Прекращаем после последнего дня месяца
                    if (day > daysInMonth)
                    {
                        break;
                    }

                    // Создаем ячейку для дня
                    Border dayCell = new Border
                    {
                        BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(2),
                        Padding = new Thickness(5)
                    };

                    // Если это текущий день, выделяем его
                    if (day == DateTime.Today.Day && _currentDate.Month == DateTime.Today.Month && _currentDate.Year == DateTime.Today.Year)
                    {
                        dayCell.Background = new SolidColorBrush(Color.FromRgb(232, 240, 254));
                    }

                    Grid.SetRow(dayCell, week + 2); // +2 для заголовка месяца и заголовков дней недели
                    Grid.SetColumn(dayCell, dayOfWeek);
                    ScheduleGrid.Children.Add(dayCell);

                    // Создаем контейнер для содержимого дня
                    StackPanel dayContent = new StackPanel();

                    // Добавляем номер дня
                    TextBlock dayNumber = new TextBlock
                    {
                        Text = day.ToString(),
                        FontWeight = day == DateTime.Today.Day && _currentDate.Month == DateTime.Today.Month && _currentDate.Year == DateTime.Today.Year
                            ? FontWeights.Bold : FontWeights.Regular,
                        Margin = new Thickness(0, 0, 0, 4)
                    };
                    dayContent.Children.Add(dayNumber);

                    // Получаем занятия для этого дня
                    // Получаем занятия для этого дня
                    DateTime currentDay = new DateTime(_currentDate.Year, _currentDate.Month, day);
                    var dayItems = _scheduleItems.Where(item =>
                        item.StartDateTime?.Date == currentDay.Date).ToList();

                    // Находим соответствующие элементы ScheduleItem
                    var dayScheduleItems = dayItems.Select(item =>
                        _scheduleItemsWrapper.FirstOrDefault(si => si.ScheduleID == item.ScheduleID) ??
                        new ScheduleItem(item, GetColorForSchedule(item))).ToList();

                    // Добавляем занятия в ячейку дня (максимум 3, остальные - счетчик)
                    int maxItems = 3;
                    for (int i = 0; i < Math.Min(maxItems, dayScheduleItems.Count); i++)
                    {
                        var scheduleItem = dayScheduleItems[i];

                        // Создаем контейнер для элемента расписания в стиле карточки
                        Grid eventContainer = new Grid();
                        eventContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
                        eventContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        // Цветная полоса слева
                        Border colorStrip = new Border
                        {
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheduleItem.Color)),
                            CornerRadius = new CornerRadius(3, 0, 0, 3)
                        };
                        Grid.SetColumn(colorStrip, 0);
                        Grid.SetRowSpan(colorStrip, 1);
                        eventContainer.Children.Add(colorStrip);

                        // Содержимое элемента
                        Border contentBorder = new Border
                        {
                            Background = Brushes.White,
                            BorderThickness = new Thickness(0, 1, 1, 1),
                            BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                            CornerRadius = new CornerRadius(0, 3, 3, 0),
                            Padding = new Thickness(4)
                        };
                        Grid.SetColumn(contentBorder, 1);

                        // Текст элемента с использованием свойств ScheduleItem
                        string timeString = scheduleItem.StartDateTime?.ToString("HH:mm") ?? "";
                        TextBlock eventContent = new TextBlock
                        {
                            Text = $"{timeString} {scheduleItem.Title}",
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            FontSize = 10
                        };

                        contentBorder.Child = eventContent;
                        eventContainer.Children.Add(contentBorder);

                        // Добавляем событие нажатия с правильной сущностью Schedule
                        eventContainer.MouseLeftButtonDown += (sender, e) => EditScheduleItem(scheduleItem.Schedule);

                        // Добавляем в ячейку дня
                        dayContent.Children.Add(eventContainer);
                    }

                    dayCell.Child = dayContent;
                    day++;
                }
            }
        }

        private void EditScheduleItem(Schedules item)
        {
            // Проверяем, было ли занятие удалено или освобождено 
            bool wasOccupied = item.ClientID != null;
            int originalClientId = item.ClientID ?? 0;
            int scheduleId = item.ScheduleID; // Сохраняем ID для дальнейшего использования

            // Открываем окно редактирования занятия с передачей словаря цветов
            var editWindow = new EditScheduleWindow(_dbContext, item, _scheduleColors);
            if (editWindow.ShowDialog() == true)
            {
                try
                {
                    // Проверяем, существует ли по-прежнему элемент расписания
                    var updatedItem = _dbContext.Schedules.Find(scheduleId);

                    if (updatedItem != null)
                    {
                        // Если занятие было освобождено (клиент был удален), проверяем список ожидания
                        if (wasOccupied && (updatedItem.ClientID == null || updatedItem.ClientID != originalClientId))
                        {
                            // Проверяем, есть ли клиенты в списке ожидания
                            if (CheckAndProcessWaitingList(scheduleId))
                            {
                                // Спрашиваем пользователя, переместить ли клиента из списка ожидания
                                var result = MessageBox.Show(
                                    "Есть клиенты в списке ожидания на это занятие. Переместить первого клиента из списка ожидания?",
                                    "Список ожидания",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Question);
                                if (result == MessageBoxResult.Yes)
                                {
                                    // Перемещаем первого клиента из списка ожидания
                                    if (MoveFirstClientFromWaitingList(updatedItem))
                                    {
                                        MessageBox.Show(
                                            "Клиент из списка ожидания успешно перемещен в расписание!",
                                            "Список ожидания",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Information);
                                    }
                                }
                            }
                        }
                    }
                    // В любом случае перезагружаем расписание
                    LoadScheduleItems();
                    GenerateScheduleView();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при обновлении расписания: {ex.Message}",
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    // При ошибке также перезагружаем расписание
                    LoadScheduleItems();
                    GenerateScheduleView();
                }
            }
        }

        private void PreviousButton_Click(object sender, RoutedEventArgs e)
        {
            switch (_currentViewMode)
            {
                case ViewMode.Day:
                    _currentDate = _currentDate.AddDays(-1);
                    break;
                case ViewMode.Week:
                    _currentDate = _currentDate.AddDays(-7);
                    break;
                case ViewMode.Month:
                    _currentDate = _currentDate.AddMonths(-1);
                    break;
            }
            UpdateDateRangeText();
            GenerateScheduleView();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            switch (_currentViewMode)
            {
                case ViewMode.Day:
                    _currentDate = _currentDate.AddDays(1);
                    break;
                case ViewMode.Week:
                    _currentDate = _currentDate.AddDays(7);
                    break;
                case ViewMode.Month:
                    _currentDate = _currentDate.AddMonths(1);
                    break;
            }
            UpdateDateRangeText();
            GenerateScheduleView();
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            _currentDate = DateTime.Today;
            UpdateDateRangeText();
            GenerateScheduleView();
        }

        private void ViewModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem selectedItem = ViewModeComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                switch (selectedItem.Content.ToString())
                {
                    case "День":
                        _currentViewMode = ViewMode.Day;
                        break;
                    case "Неделя":
                        _currentViewMode = ViewMode.Week;
                        break;
                    case "Месяц":
                        _currentViewMode = ViewMode.Month;
                        break;
                }
                UpdateDateRangeText();
                GenerateScheduleView();
            }
        }

        private void AddScheduleItem_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно добавления занятия с передачей словаря цветов
            var addWindow = new EditScheduleWindow(_dbContext, null, _scheduleColors);
            if (addWindow.ShowDialog() == true)
            {
                // После успешного добавления обновляем расписание
                LoadScheduleItems();
                GenerateScheduleView();
            }
        }

        private void InitializeScheduleColors()
        {
            // Эта функция должна быть вызвана в конструкторе после инициализации _dbContext
            if (_scheduleColors == null)
            {
                _scheduleColors = new Dictionary<int, string>();
            }

            // Можно добавить код для загрузки цветов из настроек или другого источника
        }



        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем, есть ли содержимое для печати
                if (ScheduleContainerGrid.Children.Count == 0)
                {
                    MessageBox.Show("Расписание пусто. Нечего печатать.", "Информация",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Создаем и показываем окно предварительного просмотра
                var previewWindow = new PrintPreviewWindow(ScheduleContainerGrid,
                                                           "Расписание " + DateRangeTextBlock.Text);
                previewWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании предварительного просмотра: {ex.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Метод для создания копии элемента для печати
        private FrameworkElement CreatePrintCopy(FrameworkElement original)
        {
            try
            {
                // Создаем временный контейнер
                Grid printContainer = new Grid();
                printContainer.Background = Brushes.White;

                // Добавляем заголовок
                StackPanel headerPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                headerPanel.Children.Add(new TextBlock
                {
                    Text = "Расписание занятий",
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                headerPanel.Children.Add(new TextBlock
                {
                    Text = DateRangeTextBlock.Text,
                    FontSize = 14,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 5, 0, 15)
                });

                // Создаем снимок оригинального элемента
                RenderTargetBitmap rtb = new RenderTargetBitmap(
                    (int)original.ActualWidth,
                    (int)original.ActualHeight,
                    96, 96, PixelFormats.Pbgra32);
                rtb.Render(original);

                // Создаем изображение из снимка
                Image scheduleImage = new Image { Source = rtb, Margin = new Thickness(0, 0, 0, 10) };

                // Добавляем все в контейнер
                printContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                printContainer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                Grid.SetRow(headerPanel, 0);
                Grid.SetRow(scheduleImage, 1);

                printContainer.Children.Add(headerPanel);
                printContainer.Children.Add(scheduleImage);

                return printContainer;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании копии для печати: {ex.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return new Grid(); // Возвращаем пустой элемент в случае ошибки
            }
        }

        private void EmailButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем, есть ли содержимое для отправки
                if (ScheduleContainerGrid.Children.Count == 0)
                {
                    MessageBox.Show("Расписание пусто. Нечего отправлять.", "Информация",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Создаем полный элемент для снимка, включая заголовок
                StackPanel emailContent = CreateEmailContent();

                // Открываем окно отправки email
                var sendWindow = new SendScheduleWindow(_dbContext, DateRangeTextBlock.Text, emailContent);

                // Показываем окно
                bool? result = sendWindow.ShowDialog();

                if (result == true)
                {
                    // Письмо успешно отправлено
                    MessageBox.Show("Расписание успешно отправлено по электронной почте.", "Успех",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при отправке email: {ex.Message}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private StackPanel CreateEmailContent()
        {
            StackPanel emailContent = new StackPanel();
            emailContent.Background = Brushes.White;

            // Добавляем заголовок
            TextBlock headerTitle = new TextBlock
            {
                Text = "Расписание занятий",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20, 20, 20, 10)
            };
            emailContent.Children.Add(headerTitle);

            // Добавляем текущий период
            TextBlock periodText = new TextBlock
            {
                Text = DateRangeTextBlock.Text,
                FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20, 0, 20, 20)
            };
            emailContent.Children.Add(periodText);

            // Добавляем текущий вид расписания
            if (ScheduleGrid != null)
            {
                // Создаем копию сетки расписания
                Grid scheduleCopy = CloneScheduleGrid();
                emailContent.Children.Add(scheduleCopy);
            }

            // Добавляем футер
            TextBlock footer = new TextBlock
            {
                Text = $"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}",
                FontSize = 12,
                Foreground = Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(20, 10, 20, 20)
            };
            emailContent.Children.Add(footer);

            // Устанавливаем размеры для корректного снимка
            emailContent.Width = 1200;
            emailContent.Measure(new Size(1200, double.PositiveInfinity));
            emailContent.Arrange(new Rect(0, 0, 1200, emailContent.DesiredSize.Height));

            return emailContent;
        }

        // Метод для клонирования сетки расписания
        private Grid CloneScheduleGrid()
        {
            Grid clonedGrid = new Grid();
            clonedGrid.Background = ScheduleGrid.Background;
            clonedGrid.Margin = new Thickness(10);

            // Копируем определения строк
            foreach (var rowDef in ScheduleGrid.RowDefinitions)
            {
                clonedGrid.RowDefinitions.Add(new RowDefinition { Height = rowDef.Height });
            }

            // Копируем определения колонок
            foreach (var colDef in ScheduleGrid.ColumnDefinitions)
            {
                clonedGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = colDef.Width });
            }

            // Копируем все элементы
            foreach (UIElement child in ScheduleGrid.Children)
            {
                if (child is FrameworkElement element)
                {
                    try
                    {
                        // Используем XAML для клонирования визуальных элементов
                        string xaml = System.Windows.Markup.XamlWriter.Save(element);
                        using (var stringReader = new System.IO.StringReader(xaml))
                        {
                            using (var xmlReader = System.Xml.XmlReader.Create(stringReader))
                            {
                                FrameworkElement clonedElement = (FrameworkElement)System.Windows.Markup.XamlReader.Load(xmlReader);

                                // Копируем свойства привязки к сетке
                                Grid.SetRow(clonedElement, Grid.GetRow(element));
                                Grid.SetColumn(clonedElement, Grid.GetColumn(element));
                                Grid.SetRowSpan(clonedElement, Grid.GetRowSpan(element));
                                Grid.SetColumnSpan(clonedElement, Grid.GetColumnSpan(element));

                                clonedGrid.Children.Add(clonedElement);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Ошибка при клонировании элемента: {ex.Message}");
                    }
                }
            }

            return clonedGrid;
        }

        private void WaitingListButton_Click(object sender, RoutedEventArgs e)
        {
            // Открываем окно списка ожидания
            var waitingListWindow = new WaitingListWindow(_dbContext);
            waitingListWindow.ShowDialog();

            // После закрытия окна обновляем расписание (может быть изменения)
            LoadScheduleItems();
            GenerateScheduleView();
        }


        /// <summary>
        /// Автоматически проверяет список ожидания для указанного занятия
        /// </summary>
        /// <param name="scheduleID">ID занятия</param>
        /// <returns>true, если есть ожидающие клиенты</returns>
        private bool CheckAndProcessWaitingList(int scheduleID)
        {
            try
            {
                // Получаем список ожидания для данного занятия
                var waitingList = _dbContext.WaitingLists
                    .FirstOrDefault(w => w.SheduleID == scheduleID);

                if (waitingList != null)
                {
                    // Получаем первого клиента из списка ожидания
                    var waitingClient = _dbContext.WaitingListClients
                        .Where(w => w.WaitingListID == waitingList.WaitingListID &&
                                    (w.IsProcessed.HasValue == false || w.IsProcessed.Value == false))
                        .OrderBy(w => w.EnrollmentDateTime)
                        .FirstOrDefault();

                    if (waitingClient != null)
                    {
                        return true; // Есть клиенты в списке ожидания
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при проверке списка ожидания: {ex.Message}");
            }

            return false; // Нет клиентов в списке ожидания
        }

        /// <summary>
        /// Перемещает первого клиента из списка ожидания в расписание
        /// </summary>
        /// <param name="schedule">Объект расписания</param>
        /// <returns>true, если клиент был успешно перемещен</returns>
        private bool MoveFirstClientFromWaitingList(Schedules schedule)
        {
            try
            {
                // Находим список ожидания для данного занятия
                var waitingList = _dbContext.WaitingLists
                    .FirstOrDefault(w => w.SheduleID == schedule.ScheduleID);

                if (waitingList != null)
                {
                    // Находим первого ожидающего клиента (самый старый запрос)
                    var waitingClient = _dbContext.WaitingListClients
                        .Where(w => w.WaitingListID == waitingList.WaitingListID &&
                                  (w.IsProcessed.HasValue == false || w.IsProcessed.Value == false))
                        .OrderBy(w => w.EnrollmentDateTime)
                        .FirstOrDefault();

                    if (waitingClient != null)
                    {
                        // Обновляем занятие с клиентом из списка ожидания
                        schedule.ClientID = waitingClient.ClientID;
                        _dbContext.Entry(schedule).State = System.Data.Entity.EntityState.Modified;

                        // Отмечаем запись в списке ожидания как обработанную
                        waitingClient.IsProcessed = true;
                        waitingClient.Notes += $"\nАвтоматически перемещен в расписание {DateTime.Now:dd.MM.yyyy HH:mm}";
                        _dbContext.Entry(waitingClient).State = System.Data.Entity.EntityState.Modified;

                        // Сохраняем изменения
                        _dbContext.SaveChanges();
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при перемещении из списка ожидания: {ex.Message}");
                // Можно добавить логирование или показать сообщение пользователю
            }

            return false;
        }
        /// <summary>
        /// Автоматически проверяет и обрабатывает список ожидания при освобождении занятия
        /// </summary>
        private void HandleWaitingListWhenReleased(int scheduleId, int? originalClientId)
        {
            try
            {
                // Загружаем актуальную информацию о занятии
                var schedule = _dbContext.Schedules.Find(scheduleId);

                // Если занятие освободилось (не занято никем или занято другим клиентом)
                if (schedule != null && (schedule.ClientID == null || schedule.ClientID != originalClientId))
                {
                    // Проверяем, есть ли клиенты в списке ожидания
                    if (CheckAndProcessWaitingList(scheduleId))
                    {
                        // Даем сообщение пользователю и предлагаем переместить автоматически
                        var result = MessageBox.Show(
                            "Есть клиенты в списке ожидания на это занятие. Переместить первого клиента из списка ожидания?",
                            "Список ожидания", MessageBoxButton.YesNo, MessageBoxImage.Question);

                        if (result == MessageBoxResult.Yes)
                        {
                            // Перемещаем первого клиента из списка ожидания
                            if (MoveFirstClientFromWaitingList(schedule))
                            {
                                MessageBox.Show(
                                    "Клиент из списка ожидания успешно перемещен в расписание!",
                                    "Список ожидания", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при обработке списка ожидания: {ex.Message}");
            }
        }
    }
}