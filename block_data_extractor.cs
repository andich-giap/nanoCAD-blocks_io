using System;
using System.Collections.Generic;
using Teigha.DatabaseServices;
using HostMgd.ApplicationServices;

namespace NanoCadIO.BlockIO
{
	public static class BlockDataExtractor
	{
		/// <summary>
		/// Собирает данные только из указанных выделенных объектов.
		/// Если allowedBlockNames не пуст, собираются только блоки с указанными именами.
		/// </summary>
		public static List<BlockData> Extract(Document doc, IEnumerable<ObjectId> selectedIds, HashSet<string>? allowedBlockNames = null)
		{
			var result = new List<BlockData>();
			var db = doc.Database;

			using var tr = db.TransactionManager.StartTransaction();
			
			foreach (var objId in selectedIds)
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

				// Фильтрация по выбранным именам (галочкам в списке)
				if (allowedBlockNames != null && allowedBlockNames.Count > 0)
				{
					if (!allowedBlockNames.Contains(realBlockName))
						continue;
				}

				var data = new BlockData
				{
					Handle = blockRef.Handle.ToString(),
					BlockName = realBlockName
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
			
			return result;
		}
	}
}
