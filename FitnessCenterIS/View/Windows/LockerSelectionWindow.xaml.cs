using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace FitnessCenterIS.View.Windows
{
    public partial class LockerSelectionWindow : Window
    {
        private readonly BDFitnessClubDipEntities _dbContext;
        private int _selectedLockerId = -1;
        private bool _isMale;

        public int SelectedLockerId => _selectedLockerId;

        public LockerSelectionWindow(BDFitnessClubDipEntities dbContext, bool isMale = true)
        {
            InitializeComponent();
            _dbContext = dbContext;
            _isMale = isMale;

            // Устанавливаем активную вкладку в зависимости от пола клиента
            GenderTabControl.SelectedIndex = isMale ? 0 : 1;

            LoadLockers();
        }

        private void LoadLockers()
        {
            // Загружаем шкафчики мужской раздевалки
            var maleLockerRoomType = _dbContext.LockerRoomTypes.FirstOrDefault(lrt => lrt.Name == "Мужская");
            if (maleLockerRoomType != null)
            {
                var maleLockers = _dbContext.Lockers
                    .Where(l => l.LockerRoomTypeID == maleLockerRoomType.LockerRoomTypeID)
                    .ToList()
                    .OrderBy(l => int.Parse(l.KeyNumber))
                    .ToList();
                MaleLockersList.ItemsSource = maleLockers;
            }

            // Загружаем шкафчики женской раздевалки
            var femaleLockerRoomType = _dbContext.LockerRoomTypes.FirstOrDefault(lrt => lrt.Name == "Женская");
            if (femaleLockerRoomType != null)
            {
                var femaleLockers = _dbContext.Lockers
                    .Where(l => l.LockerRoomTypeID == femaleLockerRoomType.LockerRoomTypeID)
                    .ToList()
                    .OrderBy(l => int.Parse(l.KeyNumber))
                    .ToList();
                FemaleLockersList.ItemsSource = femaleLockers;
            }
        }


        private void LockerButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                // Получаем ID шкафчика из тега кнопки
                if (int.TryParse(button.Tag.ToString(), out int lockerId))
                {
                    // Проверяем, доступен ли шкафчик
                    var locker = _dbContext.Lockers.FirstOrDefault(l => l.LockerID == lockerId);
                    if (locker != null && locker.IsAvailable.HasValue && locker.IsAvailable.Value)
                    {
                        _selectedLockerId = lockerId;
                        // Визуально выделяем выбранный шкафчик
                        button.BorderBrush = Brushes.Gold;
                        button.BorderThickness = new Thickness(3);
                    }
                    else
                    {
                        MessageBox.Show("Этот шкафчик уже занят.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedLockerId > 0)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Пожалуйста, выберите шкафчик.", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
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
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isAvailable && isAvailable)
            {
                return new SolidColorBrush(Colors.Gray);
            }
            return new SolidColorBrush(Colors.LightGray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
