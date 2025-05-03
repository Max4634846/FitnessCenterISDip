using FitnessCenterIS.Model;
using FitnessCenterIS.View.Windows;
using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace FitnessCenterIS.View.Pages
{
    public partial class RolesPage : Page
    {
        private readonly BDFitnessClubDipEntities _dbContext = new BDFitnessClubDipEntities();
        private ObservableCollection<RoleViewModel> _rolesData;

        // ID ролей, которые нельзя удалять или изменять
        private readonly int[] _protectedRoleIds = new int[0]; // будут заполнены в конструкторе

        // Класс модели представления для ролей
        public class RoleViewModel
        {
            public int RoleID { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public int StaffCount { get; set; } // Количество сотрудников с данной ролью
        }

        public RolesPage()
        {
            InitializeComponent();
            // Получаем ID ролей, которые нужно защитить (Администратор стойки и Системный администратор)
            _protectedRoleIds = GetProtectedRoleIds();
            LoadRolesData();
        }

        private int[] GetProtectedRoleIds()
        {
            return _dbContext.Roles
                .Where(r => r.Name == "Администратор стойки" || r.Name == "Системный администратор")
                .Select(r => r.RoleID)
                .ToArray();
        }

        private void LoadRolesData()
        {
            // Загружаем все роли из базы данных (кроме скрытых административных ролей)
            // и подсчитываем количество сотрудников для каждой роли
            var roles = _dbContext.Roles
                .Where(r => !_protectedRoleIds.Contains(r.RoleID)) // Исключаем защищенные роли
                .Select(r => new RoleViewModel
                {
                    RoleID = r.RoleID,
                    Name = r.Name,
                    Description = r.Description,
                    StaffCount = r.Staffs.Count
                })
                .ToList();

            _rolesData = new ObservableCollection<RoleViewModel>(roles);
            BDRoles.ItemsSource = _rolesData;
        }

        private void AddNewRole_Click(object sender, RoutedEventArgs e)
        {
            var window = new AddEditRoleWindow();
            if (window.ShowDialog() == true)
            {
                LoadRolesData(); // Перезагружаем данные после добавления
            }
        }

        private void EditRole_Click(object sender, RoutedEventArgs e)
        {
            var selectedRole = BDRoles.SelectedItem as RoleViewModel;
            if (selectedRole == null)
            {
                MessageBox.Show("Выберите роль для редактирования");
                return;
            }

            // Проверяем, является ли роль защищенной
            if (_protectedRoleIds.Contains(selectedRole.RoleID))
            {
                MessageBox.Show("Редактирование этой роли запрещено системой", "Ограничение доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var window = new AddEditRoleWindow(selectedRole.RoleID);
            if (window.ShowDialog() == true)
            {
                LoadRolesData(); // Перезагружаем данные после редактирования
            }
        }

        private void DeleteRole_Click(object sender, RoutedEventArgs e)
        {
            var selectedRole = BDRoles.SelectedItem as RoleViewModel;
            if (selectedRole == null)
            {
                MessageBox.Show("Выберите роль для удаления");
                return;
            }

            // Проверяем, является ли роль защищенной
            if (_protectedRoleIds.Contains(selectedRole.RoleID))
            {
                MessageBox.Show("Удаление этой роли запрещено системой", "Ограничение доступа", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Проверяем, используется ли роль сотрудниками
            if (selectedRole.StaffCount > 0)
            {
                MessageBox.Show($"Невозможно удалить роль, так как она назначена {selectedRole.StaffCount} сотрудникам",
                    "Ошибка удаления", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show("Вы уверены, что хотите удалить эту роль?", "Подтверждение удаления",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    var roleToDelete = _dbContext.Roles.Find(selectedRole.RoleID);
                    if (roleToDelete != null)
                    {
                        _dbContext.Roles.Remove(roleToDelete);
                        _dbContext.SaveChanges();
                        LoadRolesData(); // Перезагружаем данные после удаления
                        MessageBox.Show("Роль успешно удалена", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при удалении роли: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = SearchTextBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(searchText))
            {
                BDRoles.ItemsSource = _rolesData;
                return;
            }

            var filteredRoles = _rolesData.Where(r =>
                r.Name.ToLower().Contains(searchText) ||
                (r.Description != null && r.Description.ToLower().Contains(searchText))
            ).ToList();

            BDRoles.ItemsSource = new ObservableCollection<RoleViewModel>(filteredRoles);
        }

        private void BDRoles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Можно добавить логику при выборе роли, если необходимо
        }
    }
}