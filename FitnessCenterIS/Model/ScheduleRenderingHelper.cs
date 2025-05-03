using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;

namespace FitnessCenterIS.Model
{
    /// <summary>
    /// Вспомогательный класс для преобразования UI элементов в изображения
    /// и обеспечения правильной печати расписания
    /// </summary>
    public static class ScheduleRenderingHelper
    {
        /// <summary>
        /// Преобразует UI элемент в изображение
        /// </summary>
        /// <param name="element">Элемент для преобразования</param>
        /// <param name="scale">Масштаб (1.0 = 100%)</param>
        /// <returns>Изображение в формате BitmapSource</returns>
        public static BitmapSource RenderToImage(FrameworkElement element, double scale = 1.0)
        {
            // Убедитесь, что элемент правильно измерен и расположен
            element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            element.Arrange(new Rect(0, 0, element.DesiredSize.Width, element.DesiredSize.Height));

            // Вычисляем размеры выходного изображения с учетом масштаба
            int width = (int)(element.ActualWidth * scale);
            int height = (int)(element.ActualHeight * scale);

            if (width <= 0 || height <= 0)
            {
                // Используем минимальные размеры, если элемент не имеет размеров
                width = Math.Max(width, 800);
                height = Math.Max(height, 600);
            }

            // Создаем изображение
            RenderTargetBitmap renderTarget = new RenderTargetBitmap(
                width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);

            // Рендерим элемент на изображение
            DrawingVisual visual = new DrawingVisual();
            using (DrawingContext context = visual.RenderOpen())
            {
                // Применяем масштаб если необходимо
                if (scale != 1.0)
                {
                    context.PushTransform(new ScaleTransform(scale, scale));
                }

                // Рисуем белый фон
                context.DrawRectangle(Brushes.White, null,
                    new Rect(0, 0, element.ActualWidth, element.ActualHeight));

                // Рисуем элемент
                VisualBrush brush = new VisualBrush(element);
                context.DrawRectangle(brush, null,
                    new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            }

            // Выполняем рендеринг
            renderTarget.Render(visual);

            return renderTarget;
        }

        /// <summary>
        /// Сохраняет UI элемент как PNG файл
        /// </summary>
        /// <param name="element">Элемент для сохранения</param>
        /// <param name="filePath">Путь к файлу</param>
        /// <param name="scale">Масштаб (1.0 = 100%)</param>
        public static void SaveAsPng(FrameworkElement element, string filePath, double scale = 1.0)
        {
            BitmapSource bitmap = RenderToImage(element, scale);

            using (FileStream stream = new FileStream(filePath, FileMode.Create))
            {
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                encoder.Save(stream);
            }
        }

        /// <summary>
        /// Печатает UI элемент, преобразовав его сначала в изображение
        /// </summary>
        /// <param name="element">Элемент для печати</param>
        /// <param name="documentTitle">Заголовок документа</param>
        public static void PrintElementAsImage(FrameworkElement element, string documentTitle)
        {
            try
            {
                // Создаем изображение для печати с увеличенным масштабом для лучшего качества
                BitmapSource bitmap = RenderToImage(element, 2.0);

                // Создаем временный файл для сохранения изображения
                string tempFilePath = Path.Combine(
                    Path.GetTempPath(),
                    $"Schedule_{DateTime.Now:yyyyMMdd_HHmmss}.png");

                // Сохраняем во временный файл
                using (FileStream stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(stream);
                }

                // Создаем документ для печати
                System.Windows.Documents.FixedDocument document =
                    new System.Windows.Documents.FixedDocument();

                // Создаем страницу
                System.Windows.Documents.PageContent pageContent =
                    new System.Windows.Documents.PageContent();
                System.Windows.Documents.FixedPage fixedPage =
                    new System.Windows.Documents.FixedPage();

                // Загружаем изображение
                Image image = new Image();
                BitmapImage source = new BitmapImage();
                source.BeginInit();
                source.UriSource = new Uri(tempFilePath);
                source.CacheOption = BitmapCacheOption.OnLoad; // Кэшируем, чтобы файл можно было удалить
                source.EndInit();
                image.Source = source;

                // Устанавливаем размеры
                fixedPage.Width = 794; // A4 ширина в пикселях при 96 DPI
                fixedPage.Height = 1123; // A4 высота в пикселях при 96 DPI

                // Настраиваем размер изображения, чтобы оно поместилось на странице
                double scaleX = (fixedPage.Width - 40) / bitmap.Width; // Отступы 20px с каждой стороны
                double scaleY = (fixedPage.Height - 40) / bitmap.Height;
                double scale = Math.Min(scaleX, scaleY);

                image.Width = bitmap.Width * scale;
                image.Height = bitmap.Height * scale;

                // Центрируем изображение
                Canvas.SetLeft(image, (fixedPage.Width - image.Width) / 2);
                Canvas.SetTop(image, (fixedPage.Height - image.Height) / 2);

                // Добавляем на страницу
                fixedPage.Children.Add(image);

                // Создаем печатный диалог
                System.Windows.Controls.PrintDialog printDialog =
                    new System.Windows.Controls.PrintDialog();

                if (printDialog.ShowDialog() == true)
                {
                    // Настраиваем принтер
                    printDialog.PrintTicket.PageOrientation =
                        System.Printing.PageOrientation.Landscape;

                    // Добавляем страницу в документ
                    ((System.Windows.Markup.IAddChild)pageContent).AddChild(fixedPage);
                    document.Pages.Add(pageContent);

                    // Печатаем
                    System.Windows.Xps.XpsDocumentWriter writer =
                        System.Printing.PrintQueue.CreateXpsDocumentWriter(printDialog.PrintQueue);
                    writer.Write(document);
                }

                // Удаляем временный файл
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Игнорируем ошибки при удалении файла
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати: {ex.Message}",
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
    }
}