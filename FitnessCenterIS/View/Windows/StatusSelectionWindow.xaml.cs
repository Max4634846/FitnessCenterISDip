using System.Windows;
using System.Windows.Controls;

namespace FitnessCenterIS.View.Windows
{
    public partial class StatusSelectionWindow : Window
    {
        public string SelectedStatus { get; private set; }
        private string _currentStatus;

        public StatusSelectionWindow(string currentStatus)
        {
            InitializeComponent();
            _currentStatus = currentStatus ?? "Активна";
            CurrentStatusTextBlock.Text = _currentStatus;

            // Подсвечиваем текущий статус в списке
            foreach (ListBoxItem item in StatusListBox.Items)
            {
                if (item.Content.ToString() == _currentStatus)
                {
                    item.IsSelected = true;
                    break;
                }
            }
        }

        private void StatusListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusListBox.SelectedItem is ListBoxItem selectedItem)
            {
                string newStatus = selectedItem.Content.ToString();

                // Активируем кнопку "Сохранить", только если выбран другой статус
                SaveButton.IsEnabled = newStatus != _currentStatus;
            }
            else
            {
                SaveButton.IsEnabled = false;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (StatusListBox.SelectedItem is ListBoxItem selectedItem)
            {
                SelectedStatus = selectedItem.Content.ToString();
                DialogResult = true;
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}