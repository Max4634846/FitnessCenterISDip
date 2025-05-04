using System.Windows;
using System.Windows.Documents;
using System.Windows.Controls;
using System.Windows.Markup;
using System.IO;
using System.Xml;

namespace FitnessCenterIS.View.Windows
{
    public partial class ReceiptPreviewWindow : Window
    {
        private FlowDocument _document;

        public ReceiptPreviewWindow(FlowDocument document)
        {
            InitializeComponent();
            _document = document;
            
            // Устанавливаем документ для просмотра
            ReceiptTextBox.Document = document;
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Создаем диалог печати
                System.Windows.Controls.PrintDialog printDialog = new System.Windows.Controls.PrintDialog();
                
                // Если пользователь нажал OK, печатаем документ
                if (printDialog.ShowDialog() == true)
                {
                    // Подготавливаем документ для печати
                    FlowDocument printDoc = CloneDocument(_document);
                    printDoc.PageHeight = printDialog.PrintableAreaHeight;
                    printDoc.PageWidth = printDialog.PrintableAreaWidth;
                    printDoc.PagePadding = new Thickness(50);
                    printDoc.ColumnGap = 0;
                    printDoc.ColumnWidth = printDialog.PrintableAreaWidth;

                    // Создаем пагинатор
                    IDocumentPaginatorSource paginatorSource = printDoc;
                    printDialog.PrintDocument(paginatorSource.DocumentPaginator, "Чек на оплату");
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Ошибка при печати: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        // Метод для клонирования FlowDocument для печати
        private FlowDocument CloneDocument(FlowDocument originalDoc)
        {
            // Сериализация документа в XamlPackage
            string xaml;
            using (MemoryStream ms = new MemoryStream())
            {
                XamlWriter.Save(originalDoc, ms);
                ms.Position = 0;
                using (StreamReader sr = new StreamReader(ms))
                {
                    xaml = sr.ReadToEnd();
                }
            }

            // Десериализация для создания копии
            using (StringReader stringReader = new StringReader(xaml))
            using (XmlReader xmlReader = XmlReader.Create(stringReader))
            {
                return XamlReader.Load(xmlReader) as FlowDocument;
            }
        }
    }
}