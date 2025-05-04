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
            // Загружаем все занятия из базы данных с нужными связями
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

            // Создаем случайный генератор для определения цветов
            Random random = new Random();

            foreach (var item in _scheduleItems)
            {
                // Определяем цвет для расписания: используем существующий или генерируем новый
                string color = _scheduleColors.ContainsKey(item.ScheduleID)
                    ? _scheduleColors[item.ScheduleID]
                    : GetColorForSchedule(item);

                // Создаем обертку ScheduleItem
                _scheduleItemsWrapper.Add(new ScheduleItem(item, color));

                // Сохраняем цвет для будущего использования
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
            // Открываем окно редактирования занятия с передачей словаря цветов
            var editWindow = new EditScheduleWindow(_dbContext, item, _scheduleColors);
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
            // Используем принципиально другой подход для печати
            try
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // Настраиваем параметры печати
                    printDialog.PrintTicket.PageOrientation = System.Printing.PageOrientation.Landscape;

                    // Создаем специальный элемент для печати
                    Grid printGrid = CreatePrintableGrid();

                    // Добавляем его во временный контейнер (не видимый в UI)
                    Grid printContainer = new Grid();
                    printContainer.Children.Add(printGrid);

                    // Установка размеров для корректной печати
                    printContainer.Measure(new Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));
                    printContainer.Arrange(new Rect(0, 0, printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight));

                    // Выполняем печать с использованием Visual
                    printDialog.PrintVisual(printContainer, $"Расписание {DateRangeTextBlock.Text}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати: {ex.Message}\n{ex.StackTrace}",
                                "Ошибка печати",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        // Метод для создания специальной сетки для печати
        private Grid CreatePrintableGrid()
        {
            Grid printGrid = new Grid();
            printGrid.Background = Brushes.White;

            // Добавляем заголовок
            StackPanel headerPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 0, 20)
            };

            TextBlock title = new TextBlock
            {
                Text = "Расписание занятий",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 5)
            };

            TextBlock dateRange = new TextBlock
            {
                Text = DateRangeTextBlock.Text,
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 15)
            };

            headerPanel.Children.Add(title);
            headerPanel.Children.Add(dateRange);

            // Определяем контент в зависимости от режима просмотра
            Grid contentGrid = new Grid();

            switch (_currentViewMode)
            {
                case ViewMode.Day:
                    contentGrid = CreateDayViewForPrint();
                    break;
                case ViewMode.Week:
                    contentGrid = CreateWeekViewForPrint();
                    break;
                case ViewMode.Month:
                    contentGrid = CreateMonthViewForPrint();
                    break;
            }

            // Настраиваем основную сетку
            printGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(60) }); // Для заголовка
            printGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Для контента

            Grid.SetRow(headerPanel, 0);
            Grid.SetRow(contentGrid, 1);

            printGrid.Children.Add(headerPanel);
            printGrid.Children.Add(contentGrid);

            return printGrid;
        }

        // Метод для создания представления дня для печати
        private Grid CreateDayViewForPrint()
        {
            Grid dayGrid = new Grid();
            dayGrid.Margin = new Thickness(10);

            // Настройка сетки
            dayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) }); // Заголовок дня

            // Строки для часов (с 8:00 до 20:00)
            for (int i = 8; i <= 20; i++)
            {
                dayGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
            }

            // Колонки: время и содержимое
            dayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            dayGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Заголовок дня
            TextBlock dayHeader = new TextBlock
            {
                Text = _currentDate.ToString("dddd, dd MMMM yyyy"),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5)
            };

            Border dayHeaderBorder = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Black,
                Child = dayHeader
            };

            Grid.SetRow(dayHeaderBorder, 0);
            Grid.SetColumn(dayHeaderBorder, 0);
            Grid.SetColumnSpan(dayHeaderBorder, 2);
            dayGrid.Children.Add(dayHeaderBorder);

            // Добавляем метки времени
            for (int i = 8; i <= 20; i++)
            {
                // Метка времени
                TextBlock timeLabel = new TextBlock
                {
                    Text = $"{i:00}:00",
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(5, 0, 10, 0)
                };

                Border timeBorder = new Border
                {
                    BorderThickness = new Thickness(1, 0, 1, 1),
                    BorderBrush = Brushes.Black,
                    Child = timeLabel
                };

                Grid.SetRow(timeBorder, i - 7); // -7 так как начинаем с 8:00
                Grid.SetColumn(timeBorder, 0);
                dayGrid.Children.Add(timeBorder);

                // Ячейка для контента
                Border contentCell = new Border
                {
                    BorderThickness = new Thickness(0, 0, 1, 1),
                    BorderBrush = Brushes.Black
                };

                Grid.SetRow(contentCell, i - 7);
                Grid.SetColumn(contentCell, 1);
                dayGrid.Children.Add(contentCell);
            }

            // Добавляем блоки занятий для выбранного дня
            var dayItems = _scheduleItems.Where(item =>
                item.StartDateTime?.Date == _currentDate.Date).ToList();

            foreach (var item in dayItems)
            {
                // Получаем цвет для элемента
                string itemColor = _scheduleColors.ContainsKey(item.ScheduleID)
                    ? _scheduleColors[item.ScheduleID]
                    : GetColorForSchedule(item);

                // Создаем элемент занятия
                var eventElement = CreateEventElement(item, itemColor);

                // Рассчитываем положение и размер
                int startHour = item.StartDateTime?.Hour ?? 0;
                int startMinute = item.StartDateTime?.Minute ?? 0;
                int endHour = item.EndDateTime?.Hour ?? 0;
                int endMinute = item.EndDateTime?.Minute ?? 0;

                double startRow = (startHour - 8) + (startMinute / 60.0);
                if (startRow < 0) startRow = 0;

                double duration = (endHour - startHour) + ((endMinute - startMinute) / 60.0);
                duration = Math.Max(duration, 0.5); // Минимальная высота 30 минут

                // Устанавливаем позицию
                int rowIndex = (int)startRow + 1; // +1 для заголовка
                int rowSpan = (int)Math.Ceiling(duration);

                if (rowIndex >= 0 && rowIndex < dayGrid.RowDefinitions.Count)
                {
                    Grid.SetRow(eventElement, rowIndex);
                    Grid.SetColumn(eventElement, 1);

                    // Проверяем, чтобы rowSpan не выходил за пределы сетки
                    rowSpan = Math.Min(rowSpan, dayGrid.RowDefinitions.Count - rowIndex);
                    Grid.SetRowSpan(eventElement, rowSpan);

                    dayGrid.Children.Add(eventElement);
                }
            }

            return dayGrid;
        }

        // Метод для создания представления недели для печати
        private Grid CreateWeekViewForPrint()
        {
            Grid weekGrid = new Grid();
            weekGrid.Margin = new Thickness(10);

            // Настройка сетки
            weekGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) }); // Заголовок дней

            // Строки для часов (с 8:00 до 20:00)
            for (int i = 8; i <= 20; i++)
            {
                weekGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            }

            // Колонки: время + 7 дней недели
            weekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            for (int i = 0; i < 7; i++)
            {
                weekGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Получаем начало недели
            DateTime weekStart = GetStartOfWeek(_currentDate);

            // Добавляем заголовки дней недели
            for (int i = 0; i < 7; i++)
            {
                DateTime day = weekStart.AddDays(i);

                StackPanel dayHeader = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                TextBlock dayName = new TextBlock
                {
                    Text = day.ToString("ddd"),
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                TextBlock dayNumber = new TextBlock
                {
                    Text = day.Day.ToString(),
                    FontWeight = FontWeights.Bold,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                dayHeader.Children.Add(dayName);
                dayHeader.Children.Add(dayNumber);

                Border headerBorder = new Border
                {
                    BorderThickness = new Thickness(0, 1, 1, 1),
                    BorderBrush = Brushes.Black,
                    Child = dayHeader
                };

                Grid.SetRow(headerBorder, 0);
                Grid.SetColumn(headerBorder, i + 1);
                weekGrid.Children.Add(headerBorder);
            }

            // Добавляем пустую ячейку в верхнем левом углу
            Border emptyCell = new Border
            {
                BorderThickness = new Thickness(1),
                BorderBrush = Brushes.Black
            };
            Grid.SetRow(emptyCell, 0);
            Grid.SetColumn(emptyCell, 0);
            weekGrid.Children.Add(emptyCell);

            // Добавляем метки времени и ячейки для дней
            for (int i = 8; i <= 20; i++)
            {
                // Метка времени
                TextBlock timeLabel = new TextBlock
                {
                    Text = $"{i:00}:00",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Border timeBorder = new Border
                {
                    BorderThickness = new Thickness(1, 0, 1, 1),
                    BorderBrush = Brushes.Black,
                    Child = timeLabel
                };

                Grid.SetRow(timeBorder, i - 7); // -7 так как начинаем с 8:00
                Grid.SetColumn(timeBorder, 0);
                weekGrid.Children.Add(timeBorder);

                // Ячейки для каждого дня недели
                for (int j = 0; j < 7; j++)
                {
                    Border dayCell = new Border
                    {
                        BorderThickness = new Thickness(0, 0, 1, 1),
                        BorderBrush = Brushes.Black
                    };

                    Grid.SetRow(dayCell, i - 7);
                    Grid.SetColumn(dayCell, j + 1);
                    weekGrid.Children.Add(dayCell);
                }
            }

            // Добавляем блоки занятий для всей недели
            for (int dayIndex = 0; dayIndex < 7; dayIndex++)
            {
                DateTime currentDay = weekStart.AddDays(dayIndex);

                var dayItems = _scheduleItems.Where(item =>
                    item.StartDateTime?.Date == currentDay.Date).ToList();

                foreach (var item in dayItems)
                {
                    // Получаем цвет для элемента
                    string itemColor = _scheduleColors.ContainsKey(item.ScheduleID)
                        ? _scheduleColors[item.ScheduleID]
                        : GetColorForSchedule(item);

                    // Создаем компактный элемент занятия для недельного вида
                    var eventElement = CreateCompactEventElement(item, itemColor);

                    // Рассчитываем положение
                    int startHour = item.StartDateTime?.Hour ?? 0;
                    int startMinute = item.StartDateTime?.Minute ?? 0;

                    double startRow = (startHour - 8) + (startMinute / 60.0);
                    if (startRow < 0) startRow = 0;

                    // Устанавливаем позицию
                    int rowIndex = (int)startRow + 1; // +1 для заголовка

                    if (rowIndex >= 0 && rowIndex < weekGrid.RowDefinitions.Count)
                    {
                        Grid.SetRow(eventElement, rowIndex);
                        Grid.SetColumn(eventElement, dayIndex + 1);

                        weekGrid.Children.Add(eventElement);
                    }
                }
            }

            return weekGrid;
        }

        // Метод для создания представления месяца для печати
        private Grid CreateMonthViewForPrint()
        {
            Grid monthGrid = new Grid();
            monthGrid.Margin = new Thickness(10);

            // Настройка сетки
            // Заголовок месяца
            monthGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(40) });
            // Дни недели
            monthGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });
            // Строки для недель (максимум 6)
            for (int i = 0; i < 6; i++)
            {
                monthGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(80) });
            }

            // 7 колонок для дней недели
            for (int i = 0; i < 7; i++)
            {
                monthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            // Заголовок месяца
            TextBlock monthHeader = new TextBlock
            {
                Text = _currentDate.ToString("MMMM yyyy"),
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Border monthHeaderBorder = new Border
            {
                BorderThickness = new Thickness(1, 1, 1, 0),
                BorderBrush = Brushes.Black,
                Child = monthHeader
            };

            Grid.SetRow(monthHeaderBorder, 0);
            Grid.SetColumnSpan(monthHeaderBorder, 7);
            monthGrid.Children.Add(monthHeaderBorder);

            // Заголовки дней недели
            string[] dayNames = { "Пн", "Вт", "Ср", "Чт", "Пт", "Сб", "Вс" };
            for (int i = 0; i < 7; i++)
            {
                TextBlock dayHeader = new TextBlock
                {
                    Text = dayNames[i],
                    FontWeight = FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                Border dayHeaderBorder = new Border
                {
                    BorderThickness = new Thickness(1, 1, i < 6 ? 0 : 1, 1),
                    BorderBrush = Brushes.Black,
                    Child = dayHeader
                };

                Grid.SetRow(dayHeaderBorder, 1);
                Grid.SetColumn(dayHeaderBorder, i);
                monthGrid.Children.Add(dayHeaderBorder);
            }

            // Генерация ячеек календаря
            DateTime firstDayOfMonth = new DateTime(_currentDate.Year, _currentDate.Month, 1);
            int daysInMonth = DateTime.DaysInMonth(_currentDate.Year, _currentDate.Month);

            // Определяем день недели для первого дня месяца (0 = воскресенье, 1 = понедельник, и т.д.)
            int firstDayOfWeek = (int)firstDayOfMonth.DayOfWeek;
            // Корректируем, чтобы понедельник был первым днем недели
            if (firstDayOfWeek == 0) firstDayOfWeek = 7;
            firstDayOfWeek--; // Т.к. индексация с 0

            int day = 1;
            for (int week = 0; week < 6; week++)
            {
                for (int weekDay = 0; weekDay < 7; weekDay++)
                {
                    // Пропускаем ячейки до первого дня месяца
                    if (week == 0 && weekDay < firstDayOfWeek)
                    {
                        Border emptyCell = new Border
                        {
                            BorderThickness = new Thickness(1, 0, weekDay < 6 ? 0 : 1, 1),
                            BorderBrush = Brushes.Black
                        };

                        Grid.SetRow(emptyCell, week + 2); // +2 для заголовков
                        Grid.SetColumn(emptyCell, weekDay);
                        monthGrid.Children.Add(emptyCell);
                        continue;
                    }

                    // Прекращаем после последнего дня месяца
                    if (day > daysInMonth)
                    {
                        Border emptyCell = new Border
                        {
                            BorderThickness = new Thickness(1, 0, weekDay < 6 ? 0 : 1, 1),
                            BorderBrush = Brushes.Black
                        };

                        Grid.SetRow(emptyCell, week + 2);
                        Grid.SetColumn(emptyCell, weekDay);
                        monthGrid.Children.Add(emptyCell);
                        continue;
                    }

                    // Создаем ячейку для дня
                    Grid dayCell = new Grid();

                    // Добавляем номер дня
                    TextBlock dayNumber = new TextBlock
                    {
                        Text = day.ToString(),
                        FontWeight = FontWeights.Bold,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        Margin = new Thickness(5, 5, 0, 0)
                    };

                    dayCell.Children.Add(dayNumber);

                    // Добавляем события для этого дня
                    DateTime currentDate = new DateTime(_currentDate.Year, _currentDate.Month, day);
                    var dayEvents = _scheduleItems.Where(item =>
                        item.StartDateTime?.Date == currentDate.Date).ToList();

                    StackPanel eventsPanel = new StackPanel
                    {
                        Margin = new Thickness(3, 25, 3, 3)
                    };

                    // Отображаем только 3 события максимум
                    int eventsToShow = Math.Min(dayEvents.Count, 3);
                    for (int i = 0; i < eventsToShow; i++)
                    {
                        var item = dayEvents[i];
                        // Получаем цвет для элемента
                        string itemColor = _scheduleColors.ContainsKey(item.ScheduleID)
                            ? _scheduleColors[item.ScheduleID]
                            : GetColorForSchedule(item);

                        // Создаем миниатюрный элемент события
                        var miniEvent = CreateMiniEventElement(item, itemColor);
                        eventsPanel.Children.Add(miniEvent);
                    }

                    // Если есть еще события, показываем счетчик
                    if (dayEvents.Count > 3)
                    {
                        TextBlock moreEvents = new TextBlock
                        {
                            Text = $"+еще {dayEvents.Count - 3}",
                            FontSize = 9,
                            HorizontalAlignment = HorizontalAlignment.Right,
                            Margin = new Thickness(0, 2, 5, 0)
                        };
                        eventsPanel.Children.Add(moreEvents);
                    }

                    dayCell.Children.Add(eventsPanel);

                    // Добавляем ячейку в сетку
                    Border cellBorder = new Border
                    {
                        BorderThickness = new Thickness(1, 0, weekDay < 6 ? 0 : 1, 1),
                        BorderBrush = Brushes.Black,
                        Child = dayCell
                    };

                    Grid.SetRow(cellBorder, week + 2);
                    Grid.SetColumn(cellBorder, weekDay);
                    monthGrid.Children.Add(cellBorder);

                    day++;
                }
            }

            return monthGrid;
        }

        // Вспомогательный метод для создания элемента занятия
        private Border CreateEventElement(Schedules item, string color)
        {
            // Создаем элемент занятия
            Grid eventGrid = new Grid();
            eventGrid.Margin = new Thickness(5);

            // Настраиваем колонки
            eventGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5) });
            eventGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Цветная полоса слева
            Border colorStrip = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                CornerRadius = new CornerRadius(3, 0, 0, 3)
            };
            Grid.SetColumn(colorStrip, 0);
            Grid.SetRowSpan(colorStrip, 1);
            eventGrid.Children.Add(colorStrip);

            // Контент
            StackPanel content = new StackPanel
            {
                Margin = new Thickness(5)
            };

            // Заголовок
            TextBlock title = new TextBlock
            {
                Text = item.Title ?? "",
                FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            };
            content.Children.Add(title);

            // Время
            if (item.StartDateTime.HasValue && item.EndDateTime.HasValue)
            {
                TextBlock time = new TextBlock
                {
                    Text = $"{item.StartDateTime.Value:HH:mm} - {item.EndDateTime.Value:HH:mm}",
                    Margin = new Thickness(0, 3, 0, 0)
                };
                content.Children.Add(time);
            }

            // Место
            if (item.Rooms?.Name != null)
            {
                TextBlock location = new TextBlock
                {
                    Text = item.Rooms.Name,
                    Margin = new Thickness(0, 3, 0, 0)
                };
                content.Children.Add(location);
            }

            // Тренер
            if (item.Staffs?.Persons != null)
            {
                string trainerName = $"{item.Staffs.Persons.Surname} {item.Staffs.Persons.Name}".Trim();
                if (!string.IsNullOrEmpty(trainerName))
                {
                    TextBlock trainer = new TextBlock
                    {
                        Text = trainerName,
                        Margin = new Thickness(0, 3, 0, 0)
                    };
                    content.Children.Add(trainer);
                }
            }

            // Клиент
            if (item.Clients?.Persons != null)
            {
                string clientName = $"{item.Clients.Persons.Surname} {item.Clients.Persons.Name}".Trim();
                if (!string.IsNullOrEmpty(clientName))
                {
                    TextBlock client = new TextBlock
                    {
                        Text = $"Клиент: {clientName}",
                        Margin = new Thickness(0, 3, 0, 0)
                    };
                    content.Children.Add(client);
                }
            }

            // Группа
            if (item.Groups?.Name != null)
            {
                TextBlock group = new TextBlock
                {
                    Text = $"Группа: {item.Groups.Name}",
                    Margin = new Thickness(0, 3, 0, 0)
                };
                content.Children.Add(group);
            }

            Border contentBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(0, 3, 3, 0),
                Child = content
            };
            Grid.SetColumn(contentBorder, 1);
            eventGrid.Children.Add(contentBorder);

            // Общий контейнер
            Border container = new Border
            {
                Background = Brushes.White,
                BorderThickness = new Thickness(0),
                CornerRadius = new CornerRadius(3),
                Margin = new Thickness(2),
                Child = eventGrid
            };

            return container;
        }


        // Компактный элемент для недельного вида
        private Border CreateCompactEventElement(Schedules item, string color)
        {
            Grid eventGrid = new Grid();
            eventGrid.Margin = new Thickness(2);

            eventGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
            eventGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Цветная полоса
            Border colorStrip = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)),
                CornerRadius = new CornerRadius(2, 0, 0, 2)
            };
            Grid.SetColumn(colorStrip, 0);
            eventGrid.Children.Add(colorStrip);

            // Компактный контент
            TextBlock content = new TextBlock
            {
                Text = item.StartDateTime?.ToString("HH:mm") + " " + (item.Title ?? ""),
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(3, 1, 1, 1),
                FontSize = 9
            };

            Border contentBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(0, 2, 2, 0),
                Child = content
            };
            Grid.SetColumn(contentBorder, 1);
            eventGrid.Children.Add(contentBorder);

            // Общий контейнер
            Border container = new Border
            {
                Background = Brushes.White,
                BorderThickness = new Thickness(0.5),
                BorderBrush = Brushes.LightGray,
                CornerRadius = new CornerRadius(2),
                Child = eventGrid
            };

            return container;
        }

        // Миниатюрный элемент для месячного вида
        private Border CreateMiniEventElement(Schedules item, string color)
        {
            Grid eventGrid = new Grid();

            eventGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2) });
            eventGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Цветная полоса
            Border colorStrip = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color))
            };
            Grid.SetColumn(colorStrip, 0);
            eventGrid.Children.Add(colorStrip);

            // Минимальный контент
            TextBlock content = new TextBlock
            {
                Text = item.StartDateTime?.ToString("HH:mm") + " " + (item.Title ?? ""),
                TextWrapping = TextWrapping.NoWrap,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(2, 0, 0, 0),
                FontSize = 8
            };
            Grid.SetColumn(content, 1);
            eventGrid.Children.Add(content);

            // Общий контейнер
            Border container = new Border
            {
                Background = Brushes.White,
                BorderThickness = new Thickness(0.5),
                BorderBrush = Brushes.LightGray,
                Margin = new Thickness(0, 1, 0, 1),
                Child = eventGrid,
                Height = 14
            };

            return container;
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {

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

    }
}