using System;
using System.Collections.Generic;
using Teigha.DatabaseServices;
using HostMgd.ApplicationServices;

namespace NanoCadIO.BlockIO
{
	public static class BlockDataExtractor
	{
		/// <summary>
		/// Собирает данные со всех блоков в текущем пространстве.
		/// Если allowedBlockNames не пуст, собираются только блоки с указанными именами.
		/// </summary>
		public static List<BlockData> Extract(Document doc, HashSet<string>? allowedBlockNames = null)
		{
			var result = new List<BlockData>();
			var db = doc.Database;

			using var tr = db.TransactionManager.StartTransaction();
			var spaceId = db.CurrentSpaceId;
			var space = (BlockTableRecord)tr.GetObject(spaceId, OpenMode.ForRead);

			foreach (var objId in space)
			{
				if (tr.GetObject(objId, OpenMode.ForRead) is not BlockReference blockRef)
					continue;

				// Фильтрация по выбранным именам
				if (allowedBlockNames != null && allowedBlockNames.Count > 0)
				{
					if (!allowedBlockNames.Contains(blockRef.Name))
						continue;
				}

				var data = new BlockData
				{
					Handle = blockRef.Handle.ToString(),
					BlockName = blockRef.Name
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
			
			// tr.Commit() не нужен для read-only операций
			return result;
		}
	}
}
