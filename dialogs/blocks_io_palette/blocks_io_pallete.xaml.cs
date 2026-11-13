using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Teigha.DatabaseServices;
using NanoCadIO.BlockIO;


namespace NanoCadIO
{
	public partial class BlocksIoPalette : System.Windows.Controls.UserControl
	{
		public ObservableCollection<BlockSelectionItem> BlockItems { get; set; } = new();

		public BlocksIoPalette()
		{
			InitializeComponent();
			BlocksList.ItemsSource = BlockItems;

			// Автоматически загружаем блоки при открытии панели
			Loaded += (s, e) => RefreshBlockList();
		}

		private void RefreshBlockList()
		{
			BlockItems.Clear();
			var doc = HostMgd.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
			if (doc == null) return;

			try
			{
				using var tr = doc.Database.TransactionManager.StartTransaction();
				var blockTable = (BlockTable)tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead);

				foreach (ObjectId btrId in blockTable)
				{
					var btr = (BlockTableRecord)tr.GetObject(btrId, OpenMode.ForRead);

					// Пропускаем листы (Layouts) и системные/анонимные блоки (начинаются с *)
					if (btr.IsLayout || btr.Name.StartsWith("*") || btr.Name.StartsWith("_"))
						continue;

					BlockItems.Add(new BlockSelectionItem { BlockName = btr.Name, IsSelected = false });
				}
				Log($"Загружено {BlockItems.Count} определений блоков.");
			}
			catch (Exception ex)
			{
				Log($"Ошибка загрузки списка: {ex.Message}");
			}
		}

		private void RefreshBtn_Click(object sender, RoutedEventArgs e) => RefreshBlockList();

		private void ExportBtn_Click(object sender, RoutedEventArgs e)
		{
			var doc = HostMgd.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
			if (doc == null) { Log("Нет активного документа."); return; }

			var selectedBlocks = BlockItems.Where(b => b.IsSelected).Select(b => b.BlockName).ToHashSet();
			if (selectedBlocks.Count == 0)
			{
				Log("Не выбрано ни одного блока для экспорта.");
				return;
			}

			var saveDialog = new Microsoft.Win32.SaveFileDialog
			{
				Filter = "Excel Files (*.xlsx)|*.xlsx",
				DefaultExt = "xlsx",
				FileName = "blocks_export.xlsx"
			};

			if (saveDialog.ShowDialog() != true) return;

			try
			{
				var blocks = BlockDataExtractor.Extract(doc, selectedBlocks);
				if (blocks.Count == 0)
				{
					Log("Не найдено ни одного вхождения выбранных блоков в текущем пространстве.");
					return;
				}

				ExcelExporter.Export(blocks, saveDialog.FileName);
				Log($"Успешно экспортировано {blocks.Count} вхождений в {saveDialog.FileName}");
			}
			catch (Exception ex)
			{
				Log($"Ошибка экспорта: {ex.Message}");
			}
		}

		private void ImportBtn_Click(object sender, RoutedEventArgs e)
		{
			var doc = HostMgd.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
			if (doc == null) { Log("Нет активного документа."); return; }

			var openDialog = new Microsoft.Win32.OpenFileDialog
			{
				Filter = "Excel Files (*.xlsx)|*.xlsx",
				DefaultExt = "xlsx"
			};

			if (openDialog.ShowDialog() != true) return;

			try
			{
				int updated = ExcelImporter.Import(doc, openDialog.FileName);
				Log($"Импорт завершён. Обновлено блоков: {updated}");
			}
			catch (Exception ex)
			{
				Log($"Ошибка импорта: {ex.Message}");
			}
		}

		private void Log(string message)
		{
			LogTextBox.AppendText($"{DateTime.Now:HH:mm:ss} — {message}\n");
			LogTextBox.ScrollToEnd();
		}
	}
}
