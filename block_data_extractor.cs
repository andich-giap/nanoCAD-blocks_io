using System;
using System.Collections.Generic;
using Teigha.DatabaseServices;
using HostMgd.ApplicationServices;

namespace NanoCadIO.BlockIO
{
	public static class BlockDataExtractor
	{
		/// <summary>
		/// Собирает данные со всех блоков во всем чертеже (Модель + все Листы).
		/// Если allowedBlockNames не пуст, собираются только блоки с указанными именами.
		/// </summary>
		public static List<BlockData> Extract(Document doc, HashSet<string>? allowedBlockNames = null)
		{
			var result = new List<BlockData>();
			var db = doc.Database;

			using var tr = db.TransactionManager.StartTransaction();
			
			// Получаем таблицу блоков и ID пространства модели
			var blockTable = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
			var modelSpaceId = blockTable[BlockTableRecord.ModelSpace];

			// Собираем ID всех пространств: Модель + все Листы
			var spaceIds = new List<ObjectId> { modelSpaceId };
			var layoutDict = (DBDictionary)tr.GetObject(db.LayoutDictionaryId, OpenMode.ForRead);
			foreach (DBDictionaryEntry entry in layoutDict)
			{
				var layout = (Layout)tr.GetObject(entry.Value, OpenMode.ForRead);
				if (layout.BlockTableRecordId != modelSpaceId) // Исключаем дубликат модели
					spaceIds.Add(layout.BlockTableRecordId);
			}

			foreach (var spaceId in spaceIds)
			{
				var space = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);
				
				foreach (var objId in space)
				{
					if (tr.GetObject(objId, OpenMode.ForRead) is not BlockReference blockRef)
						continue;

					// Получаем настоящее имя блока (даже если он динамический)
					string realBlockName = blockRef.Name;
					if (blockRef.IsDynamicBlock)
					{
						var dynBtr = (BlockTableRecord)tr.GetObject(blockRef.DynamicBlockTableRecord, OpenMode.ForRead);
						realBlockName = dynBtr.Name;
					}

					// Фильтрация по выбранным именам
					if (allowedBlockNames != null && allowedBlockNames.Count > 0)
					{
						if (!allowedBlockNames.Contains(realBlockName))
							continue;
					}

					var data = new BlockData
					{
						Handle = blockRef.Handle.ToString(),
						BlockName = realBlockName // Записываем оригинальное имя
					};

					// Атрибуты
					foreach (ObjectId attId in blockRef.AttributeCollection)
					{
						if (tr.GetObject(attId, OpenMode.ForRead) is AttributeReference attRef)
						{
							data.Attributes[attRef.Tag] = attRef.TextString;
						}
					}

					// Динамические свойства
					if (blockRef.IsDynamicBlock)
					{
						var dynProps = blockRef.DynamicBlockReferencePropertyCollection;
						foreach (DynamicBlockReferenceProperty prop in dynProps)
						{
							if (prop == null || !prop.Show || prop.ReadOnly)
								continue;

							var val = prop.Value;
							string value = val switch
							{
								object[] arr => string.Join(", ", arr),
								null => string.Empty,
								_ => val.ToString() ?? string.Empty
							};
							data.DynamicProperties[prop.PropertyName] = value;
						}
					}

					result.Add(data);
				}
			}
			
			return result;
		}
	}
}
