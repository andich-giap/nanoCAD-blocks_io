using System;
using System.Windows;
using HostMgd.ApplicationServices;
using Microsoft.Win32; // Для OpenFileDialog / SaveFileDialog

namespace NanoCadIO
{
	public partial class BlocksIoDialog : Window
	{
		public BlocksIoDialog()
		{
			InitializeComponent();
		}

		private void ExportButton_Click(object sender, RoutedEventArgs e)
		{
			var doc = HostMgd.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
			if (doc == null)
			{
				Log("Нет активного документа.");
				return;
			}

			var saveDialog = new Microsoft.Win32.SaveFileDialog
			{
				Filter = "Excel Files (*.xlsx)|*.xlsx",
				DefaultExt = "xlsx",
				FileName = "blocks_export.xlsx"
			};

			if (saveDialog.ShowDialog() != true)
				return;

			try
			{
				string? blockName = string.IsNullOrWhiteSpace(BlockNameTextBox.Text)
					? null 
					: BlockNameTextBox.Text.Trim();

				var blocks = BlockIO.BlockDataExtractor.Extract(doc, blockName);
				if (blocks.Count == 0)
				{
					Log("Не найдено ни одного блока (проверьте имя или текущее пространство).");
					return;
				}

				BlockIO.ExcelExporter.Export(blocks, saveDialog.FileName);
				Log($"Экспортировано {blocks.Count} блоков в {saveDialog.FileName}");
			}
			catch (Exception ex)
			{
				Log($"Ошибка экспорта: {ex.Message}");
			}
		}

		private void ImportButton_Click(object sender, RoutedEventArgs e)
		{
			var doc = HostMgd.ApplicationServices.Application.DocumentManager.MdiActiveDocument;
			if (doc == null)
			{
				Log("Нет активного документа.");
				return;
			}

			var openDialog = new Microsoft.Win32.OpenFileDialog
			{
				Filter = "Excel Files (*.xlsx)|*.xlsx",
				DefaultExt = "xlsx"
			};

			if (openDialog.ShowDialog() != true)
				return;

			try
			{
				int updated = BlockIO.ExcelImporter.Import(doc, openDialog.FileName);
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
