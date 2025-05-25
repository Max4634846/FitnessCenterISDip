// PrintPreviewWindow.xaml.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FitnessCenterIS.View.Windows
{
    public partial class PrintPreviewWindow : Window
    {
        private FrameworkElement _elementToPrint;
        private string _documentTitle;

        public PrintPreviewWindow(FrameworkElement elementToPrint, string documentTitle)
        {
            InitializeComponent();
            _elementToPrint = elementToPrint;
            _documentTitle = documentTitle;
            Title = "Предварительный просмотр: " + documentTitle;

            // Загружаем предварительный просмотр после инициализации окна
            this.Loaded += PrintPreviewWindow_Loaded;
        }

        private void PrintPreviewWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Создаем снимок элемента
                RenderTargetBitmap renderTarget = new RenderTargetBitmap(
                    (int)_elementToPrint.ActualWidth,
                    (int)_elementToPrint.ActualHeight,
                    96, 96, PixelFormats.Pbgra32);
                renderTarget.Render(_elementToPrint);

                // Создаем изображение
                Image previewImage = new Image
                {
                    Source = renderTarget,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // Добавляем только изображение, без дополнительного заголовка
                PreviewPanel.Children.Add(previewImage);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании предварительного просмотра: {ex.Message}",
                               "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Создаем диалог печати
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // Создаем элемент для печати в нужном формате
                    Grid printGrid = CreatePrintElement();

                    // Выполняем печать
                    printDialog.PrintVisual(printGrid, _documentTitle);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати: {ex.Message}",
                              "Ошибка печати", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Grid CreatePrintElement()
        {
            // Создаем снимок элемента
            RenderTargetBitmap renderTarget = new RenderTargetBitmap(
                (int)_elementToPrint.ActualWidth,
                (int)_elementToPrint.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(_elementToPrint);

            // Создаем контейнер для печати
            Grid printGrid = new Grid();
            printGrid.Background = Brushes.White;

            // Создаем изображение без дополнительного заголовка
            Image scheduleImage = new Image
            {
                Source = renderTarget,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // Добавляем изображение в сетку
            printGrid.Children.Add(scheduleImage);

            return printGrid;
        }


        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }
    }
}