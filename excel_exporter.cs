using ClosedXML.Excel;
using System.Collections.Generic;
using System.Linq;

namespace NanoCadIO.BlockIO
{
	public static class ExcelExporter
	{
		public static void Export(List<BlockData> blocks, string filePath)
		{
			using var workbook = new XLWorkbook();
			var ws = workbook.Worksheets.Add("Blocks");

			// Собираем все уникальные теги атрибутов и имена динамических свойств
			var allAttrTags = blocks.SelectMany(b => b.Attributes.Keys).Distinct().OrderBy(x => x).ToList();
			var allDynProps = blocks.SelectMany(b => b.DynamicProperties.Keys).Distinct().OrderBy(x => x).ToList();

			// Заголовки
			ws.Cell(1, 1).Value = "Handle";
			ws.Cell(1, 2).Value = "BlockName";
			
			int col = 3;
			foreach (var tag in allAttrTags)
				ws.Cell(1, col++).Value = $"Attr_{tag}";
			foreach (var prop in allDynProps)
				ws.Cell(1, col++).Value = $"Dyn_{prop}";

			// Данные
			int row = 2;
			foreach (var block in blocks)
			{
				ws.Cell(row, 1).Value = block.Handle;
				ws.Cell(row, 2).Value = block.BlockName;

				int c = 3;
				foreach (var tag in allAttrTags)
				{
					ws.Cell(row, c++).Value = block.Attributes.GetValueOrDefault(tag, string.Empty);
				}
				foreach (var prop in allDynProps)
				{
					ws.Cell(row, c++).Value = block.DynamicProperties.GetValueOrDefault(prop, string.Empty);
				}
				row++;
			}

			// Автоширина и стиль заголовков
			ws.Columns().AdjustToContents();
			ws.Row(1).Style.Font.Bold = true;

			workbook.SaveAs(filePath);
		}
	}
}
