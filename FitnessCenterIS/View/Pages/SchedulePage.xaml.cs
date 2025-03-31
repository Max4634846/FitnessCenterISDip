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
            LoadScheduleItems();
            UpdateDateRangeText();
            GenerateScheduleView();
            this.Loaded += SchedulePage_Loaded;
        }

        // Новый конструктор с указанием начального режима отображения
        public SchedulePage(ViewMode initialViewMode)
        {
            InitializeComponent();
            _dbContext = new BDFitnessClubDipEntities();
            _currentViewMode = initialViewMode;
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
            // Загружаем все занятия из базы данных
            var allScheduleItems = _dbContext.Schedules
                .Include("Rooms")
                .Include("Staffs.Persons")
                .Include("Clients.Persons")
                .Include("Groups")
                .ToList();


            // Фильтруем только активные занятия для отображения
            _scheduleItems = allScheduleItems
                .Where(item => item.ScheduleStatus == null || item.ScheduleStatus == "Активно")
                .ToList();

            _scheduleItemsWrapper.Clear();

            foreach (var item in _scheduleItems)
            {
                string color = _scheduleColors.ContainsKey(item.ScheduleID)
                    ? _scheduleColors[item.ScheduleID]
                    : GetColorForSchedule(item);

                _scheduleItemsWrapper.Add(new ScheduleItem(item, color));
                _scheduleColors[item.ScheduleID] = color;
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

            // Цвет по умолчанию
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
            TextBlock timeText = new TextBlock
            {
                Text = $"{item.StartDateTime:HH:mm} - {item.EndDateTime:HH:mm}",
                FontSize = 11,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 1, 0, 0)
            };
            content.Children.Add(timeText);

            // Место проведения
            if (item.Rooms?.Name != null)
            {
                TextBlock locationText = new TextBlock
                {
                    Text = item.Rooms.Name,
                    FontSize = 11,
                    Foreground = Brushes.Black,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                content.Children.Add(locationText);
            }

            // Тренер
            if (item.Staffs?.Persons != null)
            {
                string trainerName = $"{item.Staffs.Persons.Surname} {item.Staffs.Persons.Name}".Trim();
                if (!string.IsNullOrEmpty(trainerName))
                {
                    TextBlock trainerText = new TextBlock
                    {
                        Text = trainerName,
                        FontSize = 11,
                        Foreground = Brushes.Black,
                        Margin = new Thickness(0, 1, 0, 0)
                    };
                    content.Children.Add(trainerText);
                }
            }
            // Добавляем информацию о клиенте, если он есть
            if (item.Clients?.Persons != null)
            {
                string clientName = $"{item.Clients.Persons.Surname} {item.Clients.Persons.Name}".Trim();
                if (!string.IsNullOrEmpty(clientName))
                {
                    TextBlock clientText = new TextBlock
                    {
                        Text = $"Клиент: {clientName}",
                        Foreground = Brushes.Black,
                        Margin = new Thickness(0, 1, 0, 0)
                    };
                    content.Children.Add(clientText);
                }
            }

            // Добавляем информацию о группе, если она есть
            if (item.Groups?.Name != null)
            {
                TextBlock groupText = new TextBlock
                {
                    Text = $"Группа: {item.Groups.Name}",
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
                    DateTime currentDay = new DateTime(_currentDate.Year, _currentDate.Month, day);
                    var dayItems = _scheduleItems.Where(item =>
                        item.StartDateTime?.Date == currentDay.Date).ToList();

                    // Добавляем занятия в ячейку дня (максимум 3, остальные - счетчик)
                    int maxItems = 3;
                    for (int i = 0; i < Math.Min(maxItems, dayItems.Count); i++)
                    {
                        var item = dayItems[i];
                        string scheduleColor = "#607D8B"; // Серый по умолчанию

                        if (_scheduleColors.ContainsKey(item.ScheduleID))
                        {
                            scheduleColor = _scheduleColors[item.ScheduleID];
                        }

                        // Создаем контейнер для элемента расписания в стиле карточки
                        Grid eventContainer = new Grid();
                        eventContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
                        eventContainer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        // Цветная полоса слева
                        Border colorStrip = new Border
                        {
                            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(scheduleColor)),
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

                        // Текст элемента
                        TextBlock eventContent = new TextBlock
                        {
                            Text = $"{item.StartDateTime:HH:mm} {item.Title}",
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            FontSize = 10
                        };

                        contentBorder.Child = eventContent;
                        eventContainer.Children.Add(contentBorder);

                        // Добавляем событие нажатия
                        eventContainer.MouseLeftButtonDown += (sender, e) => EditScheduleItem(item);

                        // Добавляем в ячейку дня
                        dayContent.Children.Add(eventContainer);
                    }

                    // Если есть еще занятия, добавляем счетчик
                    if (dayItems.Count > maxItems)
                    {
                        TextBlock moreText = new TextBlock
                        {
                            Text = $"+ еще {dayItems.Count - maxItems}",
                            Foreground = new SolidColorBrush(Color.FromRgb(66, 66, 66)),
                            FontSize = 10,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Thickness(0, 2, 0, 0)
                        };
                        dayContent.Children.Add(moreText);
                    }

                    dayCell.Child = dayContent;
                    day++;
                }
            }
        }

        private void EditScheduleItem(Schedules item)
        {
            // Открываем окно редактирования занятия без передачи цветов
            var editWindow = new EditScheduleWindow(_dbContext, item);
            if (editWindow.ShowDialog() == true)
            {
                // После успешного редактирования обновляем расписание
                LoadScheduleItems();
                GenerateScheduleView();
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
            // Открываем окно добавления занятия без передачи цветов
            var addWindow = new EditScheduleWindow(_dbContext, null);
            if (addWindow.ShowDialog() == true)
            {
                // После успешного добавления обновляем расписание
                LoadScheduleItems();
                GenerateScheduleView();
            }
        }



        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                printDialog.PrintVisual(ScheduleGrid, "Расписание");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
