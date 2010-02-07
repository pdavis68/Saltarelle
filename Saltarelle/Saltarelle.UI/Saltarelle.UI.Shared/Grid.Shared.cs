#if SERVER
using RowDataList = System.Collections.Generic.List<Saltarelle.UI.GridRowData>;
using System.Text;
using System;
using System.Collections.Generic;
#elif CLIENT
using RowDataList = System.ArrayList;
using System;
using System.DHTML;
#endif

namespace Saltarelle.UI {
	[Record]
	internal sealed class GridRowData {
		public string[] cellTexts;
		public object data;
		public GridRowData(string[] cellTexts, object data) {
			this.cellTexts = cellTexts;
			this.data = data;
		}
	}

	#if CLIENT
	public class GridSelectionChangingEventArgs : EventArgs {
		public int OldSelectionIndex;
		public int NewSelectionIndex;
		public bool Cancel;
	}
	public delegate void GridSelectionChangingEventHandler(object sender, GridSelectionChangingEventArgs e);
	
	public class GridCellClickedEventArgs : EventArgs {
		public int Row;
		public int Col;
		public bool PreventRowSelect;
	}
	public delegate void GridCellClickedEventHandler(object sender, GridCellClickedEventArgs e);
	
	public class GridKeyPressEventArgs : EventArgs {
		public int KeyCode;
		public bool PreventDefault;
	}
	public delegate void GridKeyPressEventHandler(object sender, GridKeyPressEventArgs e);
	
	public class GridDragDropCompletingEventArgs : CancelEventArgs {
		public int ItemIndex;
		public int DropIndex;
		public GridDragDropCompletingEventArgs(int itemIndex, int dropIndex) {
			this.ItemIndex = itemIndex;
			this.DropIndex = dropIndex;
			this.Cancel    = false;
		}
	}
	public delegate void GridDragDropCompletingEventHandler(object sender, GridDragDropCompletingEventArgs e);

	public class GridDragDropCompletedEventArgs : EventArgs {
		public int ItemIndex;
		public int DropIndex;
		public GridDragDropCompletedEventArgs(int itemIndex, int dropIndex) {
			this.ItemIndex = itemIndex;
			this.DropIndex = dropIndex;
		}
	}
	public delegate void GridDragDropCompletedEventHandler(object sender, GridDragDropCompletedEventArgs e);
	#endif

	public class Grid : IControl, IClientCreateControl, IResizableX, IResizableY {
		public const int BORDER_SIZE = 1;
	
		public const string DIV_CLASS = "Grid ui-widget-content";
		public const string DISABLED_DIV_CLASS = "DisabledGrid";
		public const string SPACER_TH_CLASS = "spacer";
		public const string HEADER_DIV_CLASS = "GridHeader";
		public const string HEADER_TABLE_CLASS = "GridHeader";
		public const string VALUES_DIV_CLASS = "GridValues";
		public const string VALUES_TABLE_CLASS = "GridValues";
		public const string EVEN_ROW_CLASS = "GridRowEven";
		public const string ODD_ROW_CLASS = "GridRowOdd";
		public const string ROW_HOVER_CLASS = "DropHover";
		public const string CURRENT_DRAGGING_ROW_CLASS = "CurrentDraggingRow";
	
		private string id;
		private int[] colWidths = new int[0];
		private string[] colClasses = new string[0];
		private string[] colTitles = new string[0];
		private RowDataList rowsIfNotRendered = new RowDataList();
		private Position position;
		private int width;
		private int height;
		private int tabIndex;
		private int numRows;
		private bool enabled = true;
		private bool colHeadersVisible = true;
		private bool enableDragDrop = false;
		private int selectedRowIndex = -1;
		
		#if CLIENT
			private jQuery element;
			private bool rebuilding;
			private int headerHeight;
			
			private jQuery headerTr;
			private jQuery valuesTbody;
		#endif

		public string Id {
			get { return id; }
			set {
				id = value;
				#if CLIENT
					if (element != null)
						element.attr("id", value);
				#endif
			}
		}
		
		public int TabIndex {
			get { return tabIndex; }
			set {
				#if CLIENT
					if (element != null && enabled)
						element.attr("tabindex", value);
				#endif
				tabIndex = value;
			}
		}
		
		public Position Position {
			get {
				#if CLIENT
					return element != null ? PositionHelper.GetPosition(element) : position;
				#else
					return position;
				#endif
			}
			set {
				position = value;
				#if CLIENT
					if (element != null)
						PositionHelper.ApplyPosition(element, value);
				#endif
			}
		}

		public int Width {
			get {
				return width;
			}
			set {
				#if CLIENT
					if (element != null) {
						element.children("div").andSelf().width(value - 2 * BORDER_SIZE);
					}
				#endif
				width = value;
			}
		}
		public int MinWidth { get { return 10; } }
		public int MaxWidth { get { return 1000; } }

		public int Height {
			get {
				return height;
			}
			set {
				#if CLIENT
					if (element != null)
						element.children("div:eq(1)").height(value - 2 * BORDER_SIZE - (colHeadersVisible ? headerHeight : 0));
				#endif
				height = value;
			}
		}
		public int MinHeight { get { return 10; } }
		public int MaxHeight { get { return 1000; } }

		public int NumColumns {
			get {
				return colWidths.Length;
			}
			set {
				if (value == colWidths.Length)
					return;
			
				if (NumRows > 0)
					throw new Exception("Can only change number of columns when the grid is empty");
			
				colWidths = (int[])Utils.ArrayResize(colWidths, value, 100);
				colTitles = (string[])Utils.ArrayResize(colTitles, value, "");
				colClasses = (string[])Utils.ArrayResize(colClasses, value, "");
				#if CLIENT
					if (element != null) {
						element.html(InnerHtml);
						AttachInner();
					}
				#endif
			}
		}

		public void SetColTitle(int col, string title) {
			#if CLIENT
				if (element != null)
					headerTr.children("th:eq(" + col.ToString() + ")").children("div").children("div:eq(0)").text(title);
			#endif
			colTitles[col] = title;
		}
		
		public string GetColTitle(int col) {
			return colTitles[col];
		}
		
		public string[] ColTitles {
			get {
				return (string[])colTitles.Clone();
			}
			set {
				if (value.Length != NumColumns)
					NumColumns = value.Length;
				for (int i = 0; i < NumColumns; i++)
					SetColTitle(i, value[i]);
			}
		}

		public void SetColWidth(int col, int width) {
			#if CLIENT
				if (element != null) {
					element.children("div").children("table").children("thead,tbody").children("tr").children("th,td").filter(":nth-child(" + (col + 1).ToString() + ")").children("div").width(width);
					element.children("div:eq(1)").scroll();
				}
			#endif
			colWidths[col] = width;
		}
		
		public int GetColWidth(int col) {
			return colWidths[col];
		}
		
		public int[] ColWidths {
			get {
				int[] result = new int[NumColumns];
				for (int i = 0; i < result.Length; i++)
					result[i] = GetColWidth(i);
				return result;
			}
			set {
				if (value.Length != NumColumns)
					NumColumns = value.Length;
				for (int i = 0; i < NumColumns; i++)
					SetColWidth(i, value[i]);
			}
		}
		
		public void SetColClass(int col, string cls) {
			#if CLIENT
				if (element != null) {
					jQuery cells = valuesTbody.children("tr").children("td:nth-child(" + (col + 1).ToString() + ")");
					if (!string.IsNullOrEmpty(colClasses[col]))
						cells.removeClass(colClasses[col]);
					if (!string.IsNullOrEmpty(cls))
						cells.addClass(cls);
				}
			#endif
			colClasses[col] = cls;
		}
		
		public string GetColClass(int col) {
			return colClasses[col];
		}
		
		public string[] ColClasses {
			get {
				string[] result = new string[NumColumns];
				for (int i = 0; i < result.Length; i++)
					result[i] = GetColClass(i);
				return result;
			}
			set {
				if (value.Length != NumColumns)
					NumColumns = value.Length;
				for (int i = 0; i < NumColumns; i++)
					SetColClass(i, value[i]);
			}
		}
		
		public bool Enabled {
			get { return enabled; }
			set {
				#if CLIENT
					if (element != null && value != enabled) {
						if (selectedRowIndex != -1) {
							if (value && enableDragDrop) {
								MakeDraggable(SelectedRow);
								EnableDroppableRows(valuesTbody.children());
								EnableDroppableValueDiv();
							}
							else {
								SelectedRow.draggable("destroy");
								valuesTbody.children().droppable("destroy");
								element.children("div:eq(1)").droppable("destroy");
							}
						}

						if (value) {
							element.removeClass(DISABLED_DIV_CLASS);
							element.attr("tabindex", tabIndex);
						}
						else {
							element.addClass(DISABLED_DIV_CLASS);
							element.removeAttr("tabindex");
						}
					}
				#endif						
				enabled = value;
			}
		}
		
		public bool ColHeadersVisible {
			get { return colHeadersVisible; }
			set {
				colHeadersVisible = value;
				#if CLIENT
					if (element != null) {
						element.children("div:eq(0)").css("display", colHeadersVisible ? "" : "none");
						element.children("div:eq(1)").height(height - 2 * BORDER_SIZE - (colHeadersVisible ? headerHeight : 0));
					}
				#endif
			}
		}
		
		public bool EnableDragDrop {
			get {
				return enableDragDrop;
			}
			set {
				#if CLIENT
					if (element != null && value != enableDragDrop) {
						if (selectedRowIndex != -1) {
							if (value && enabled) {
								MakeDraggable(SelectedRow);
								EnableDroppableRows(valuesTbody.children());
								EnableDroppableValueDiv();
							}
							else {
								SelectedRow.draggable("destroy");
								valuesTbody.children().droppable("destroy");
								element.children("div:eq(1)").droppable("destroy");
							}
						}
					}
				#endif
				enableDragDrop = value;
			}
		}

		public void AddItem(string[] cellTexts, object data) {
			InsertItem(numRows, cellTexts, data);
		}
		
		public void InsertItem(int index, string[] cellTexts, object data) {
			numRows++;
			if (selectedRowIndex >= index)
				selectedRowIndex++;
			#if CLIENT
				if (element != null && !rebuilding) {
					StringBuilder sb = new StringBuilder();
					AddRowHtml(sb, cellTexts, (numRows % 2) == 1, false, null);
					jQuery q = JQueryProxy.jQuery(sb.ToString());
					Type.SetField(q.get(0), "__data", data);
					q.click(rowClickHandler);
					if (enabled && enableDragDrop)
						EnableDroppableRows(q);
					if (index == numRows - 1) // remember we already incremented numRows
						q.appendTo(valuesTbody);
					else
						q.insertBefore(valuesTbody.children().eq(index));

					for (jQuery next = q.next(); next.size() > 0; next = next.next()) {
						if (next.isInExpression("." + EVEN_ROW_CLASS)) {
							next.removeClass(EVEN_ROW_CLASS);
							next.addClass(ODD_ROW_CLASS);
						}
						else {
							next.removeClass(ODD_ROW_CLASS);
							next.addClass(EVEN_ROW_CLASS);
						}
					}

					return;
				}
			#endif
			rowsIfNotRendered.Insert(index, new GridRowData(cellTexts, data));
		}
		

		public void BeginRebuild() {
			#if CLIENT
				rebuilding = true;
			#endif
			Clear();
		}
		
		public void EndRebuild() {
			#if CLIENT
				rebuilding = false;
				
				if (element != null) {
					int index = 0;
					StringBuilder sb = new StringBuilder();
					foreach (GridRowData r in rowsIfNotRendered) {
						AddRowHtml(sb, r.cellTexts, (index % 2) == 0, index == selectedRowIndex, r.data);
						index++;
					}
					valuesTbody.html(sb.ToString());

					AttachToValuesTbody();
				}
			#endif
		}
		
		public void Clear() {
			numRows = 0;
			selectedRowIndex = -1;
			#if CLIENT
				if (element != null) {
					valuesTbody.empty();
					OnSelectionChanged(EventArgs.Empty);
				}
			#endif
			rowsIfNotRendered.Clear();
		}
		
		public void UpdateItem(int row, string[] cellTexts, object data) {
			#if CLIENT
				if (element != null && !rebuilding) {
					jQuery q = valuesTbody.children(":eq(" + row.ToString() + ")");
					Type.SetField(q.get(0), "__data", data);
					q.children("td").each(delegate(int col, DOMElement e) {
						JQueryProxy.jQuery(e).children("div").children("div").text(col < cellTexts.Length ? cellTexts[col] : "");
						return true;
					});
				}
			#endif
			rowsIfNotRendered[row] = new GridRowData(cellTexts, data);
		}
		
		public void DeleteItem(int row) {
			numRows--;
			#if CLIENT
				if (element != null && !rebuilding) {
					int newSelection = SelectedRowIndex;
					bool changeSelection = false;
					if (SelectedRowIndex == row) {
						if (numRows > 0) {
							if (newSelection == numRows)
								newSelection = numRows - 1;
						}
						else
							newSelection = -1;
						changeSelection = true;
					}
					jQuery q = valuesTbody.children(":eq(" + row.ToString() + ")"), next = q.next();
					q.remove();
					for (; next.size() > 0; next = next.next()) {
						if (next.isInExpression("." + EVEN_ROW_CLASS)) {
							next.removeClass(EVEN_ROW_CLASS);
							next.addClass(ODD_ROW_CLASS);
						}
						else {
							next.removeClass(ODD_ROW_CLASS);
							next.addClass(EVEN_ROW_CLASS);
						}
					}
					if (changeSelection) {
						selectedRowIndex = -1; // hack to make the next procedure sure the GUI must be updated
						SelectedRowIndex = newSelection;
						OnSelectionChanged(EventArgs.Empty);
					}
					return;
				}
			#endif
			if (selectedRowIndex >= row)
				selectedRowIndex--;
			rowsIfNotRendered.RemoveAt(row);
		}
		
		public object GetData(int row) {
			if (row < 0 || row >= numRows)
				return null;
			#if CLIENT
				if (element != null && !rebuilding)
					return Type.GetField(((TableSectionElement)valuesTbody.get(0)).Rows[row], "__data");
			#endif
			return ((GridRowData)rowsIfNotRendered[row]).data;
		}

		public string[] GetTexts(int row) {
			if (row < 0 || row >= numRows)
				return null;
			#if CLIENT
				if (element != null && !rebuilding) {
					jQuery jq = JQueryProxy.jQuery(((TableSectionElement)valuesTbody.get(0)).Rows[row]);
					string[] result = new string[jq.children().size()];
					for (int i = 0; i < result.Length; i++)
						result[i] = jq.children().eq(i).text();
					return result;
				}
			#endif
			return ((GridRowData)rowsIfNotRendered[row]).cellTexts;
		}
		
		public int NumRows {
			get {
				return numRows;
			}
		}
		
		private void AddRowHtml(StringBuilder sb, string[] cellTexts, bool even, bool selected, object data) {
			sb.Append("<tr" + (data != null ? (" __data=\"" + Utils.HtmlEncode(Utils.Json(data)) + "\"") : "") + " class=\"" + (even ? EVEN_ROW_CLASS : ODD_ROW_CLASS) + (selected ? " ui-state-highlight" : "") + "\">");
			for (int c = 0; c < NumColumns; c++)
				sb.Append("<td " + (string.IsNullOrEmpty(colClasses[c]) ? "" : (" class=\"" + colClasses[c] + "\"")) + "><div style=\"width: " + Utils.ToStringInvariantInt(colWidths[c]) + "px\"><div>" + (c < cellTexts.Length && !string.IsNullOrEmpty(cellTexts[c]) ? Utils.HtmlEncode(cellTexts[c]) : Utils.BlankImageHtml) + "</div></div></td>");
			sb.Append("</tr>");
		}
		
		private string InnerHtml {
			get {
				StringBuilder sb = new StringBuilder();
				sb.Append("<div class=\"" + HEADER_DIV_CLASS + "\" style=\"width: " + (this.width - 2 * BORDER_SIZE) + "px\"><table cellpadding=\"0\" cellspacing=\"0\" class=\"" + HEADER_TABLE_CLASS + "\"><thead><tr>");
				for (int c = 0; c < NumColumns; c++)
					sb.Append("<th " + (string.IsNullOrEmpty(colClasses[c]) ? "" : (" class=\"" + colClasses[c] + "\"")) + "><div style=\"width: " + Utils.ToStringInvariantInt(colWidths[c]) + "px\"><div>" + (!string.IsNullOrEmpty(colTitles[c]) ? Utils.HtmlEncode(colTitles[c]) : Utils.BlankImageHtml) + "</div></div></th>");
				sb.Append("<th class=\"" + SPACER_TH_CLASS + "\"><div>&nbsp;</div></th></tr></thead></table></div><div class=\"" + VALUES_DIV_CLASS + "\" style=\"width: " + (this.width - 2 * BORDER_SIZE) + "px\"><table cellpadding=\"0\" cellspacing=\"0\" class=\"" + VALUES_TABLE_CLASS + "\"><tbody>");
				int index = 0;
				foreach (GridRowData r in rowsIfNotRendered) {
					AddRowHtml(sb, r.cellTexts, (index % 2) == 0, index == selectedRowIndex, r.data);
					index++;
				}
				sb.Append("</tbody></table></div>");
				return sb.ToString();
			}
		}
		
		protected virtual void BeforeGetHtml() {
		}

		public string Html {
			get {
				if (string.IsNullOrEmpty(id))
					throw new Exception("Must set ID before render");
				BeforeGetHtml();
				string style = PositionHelper.CreateStyle(position, width - 2 * BORDER_SIZE, -1);
				return "<div id=\"" + id + "\" class=\"" + DIV_CLASS + (enabled ? "" : (" " + DISABLED_DIV_CLASS)) + "\" style=\"" + style + "\"" + (enabled ? " tabindex=\"" + Utils.ToStringInvariantInt(tabIndex) + "\"" : "")
				#if SERVER
					 + " __cfg=\"" + Utils.HtmlEncode(Utils.Json(ConfigObject)) + "\""
				#endif
				     + ">"
				     +     InnerHtml
				     + "</div>";
			}
		}
		
		protected virtual void InitDefault() {
			position = PositionHelper.NotPositioned;
			width = 300;
			height = 300;
		}
		
#if SERVER
		public Grid() {
			GlobalServices.Provider.GetService<IScriptManagerService>().RegisterType(GetType());
			InitDefault();
		}

		protected virtual void AddItemsToConfigObject(Dictionary<string, object> config) {
			config["colTitles"] = colTitles;
			config["colWidths"] = colWidths;
			config["width"] = width;
			config["height"] = height;
			config["tabIndex"] = tabIndex;
			config["numRows"] = numRows;
			config["colClasses"] = colClasses;
			config["enabled"] = enabled;
			config["colHeadersVisible"] = colHeadersVisible;
			config["enableDragDrop"] = enableDragDrop;
			config["selectedRowIndex"] = selectedRowIndex;
		}

		private object ConfigObject {
			get {
				var config = new Dictionary<string, object>();
				AddItemsToConfigObject(config);
				return config;
			}
		}

		public int SelectedRowIndex {
			get {
				return selectedRowIndex;
			}
			set {
				if (selectedRowIndex >= numRows)
					throw new ArgumentException("value");
				selectedRowIndex = value;
			}
		}
#endif

#if CLIENT
		[AlternateSignature]
		public extern Grid();
		public Grid(string id) {
			if (!Script.IsUndefined(id)) {
				this.id = id;
				InitConfig((Dictionary)Utils.EvalJson((string)JQueryProxy.jQuery("#" + id).attr("__cfg")));
			}
			else
				InitDefault();
		}

		public event GridKeyPressEventHandler KeyPress;
		public event GridSelectionChangingEventHandler SelectionChanging;
		public event GridCellClickedEventHandler CellClicked;
		public event EventHandler SelectionChanged;
		public event GridDragDropCompletingEventHandler DragDropCompleting;
		public event GridDragDropCompletedEventHandler DragDropCompleted;
		
		protected virtual void InitConfig(Dictionary config) {
			colTitles = (string[])config["colTitles"];
			colWidths = (int[])config["colWidths"];
			colClasses = (string[])config["colClasses"];
			width = (int)config["width"];
			height = (int)config["height"];
			tabIndex = (int)config["tabIndex"];
			numRows = (int)config["numRows"];
			enabled = (bool)config["enabled"];
			colHeadersVisible = (bool)config["colHeadersVisible"];
			enableDragDrop = (bool)config["enableDragDrop"];
			selectedRowIndex = (int)config["selectedRowIndex"];

			Attach();
		}
		
		private JQueryEventHandlerDelegate rowClickHandler;

		public jQuery Element { get { return element; } }
		
		private jQuery SelectedRow { get { return valuesTbody.children().eq(selectedRowIndex); } }

		public int SelectedRowIndex {
			get {
				return selectedRowIndex;
			}
			set {
				if (selectedRowIndex == value)
					return;
				
				if (!RaiseSelectionChanging(value))
					return;
					
				if (selectedRowIndex != -1) {
					jQuery row = SelectedRow;
					row.removeClass("ui-state-highlight");
					if (enableDragDrop)
						row.draggable("destroy");
				}
				selectedRowIndex = value;
				if (selectedRowIndex != -1) {
					EnsureVisible(selectedRowIndex);
					jQuery row = SelectedRow;
					row.addClass("ui-state-highlight");
					if (enableDragDrop && enabled)
						MakeDraggable(row);
				}
				OnSelectionChanged(EventArgs.Empty);
			}
		}
		
		public void EnsureVisible(int rowIndex) {
			jQuery row = valuesTbody.children("tr:eq(" + Utils.ToStringInvariantInt(rowIndex) + ")");
			jQuery valuesDiv = element.children("div:eq(1)");
			DOMElement d = valuesDiv.get(0);
			double offsetTop = row.offset().top - valuesDiv.offset().top, scrollTop = valuesDiv.scrollTop(), rowHeight = row.height(), tblHeight = d.ClientHeight;

			if (offsetTop < 0) {
				valuesDiv.scrollTop(Math.Round(scrollTop + offsetTop));
			}
			else if (offsetTop + rowHeight > tblHeight) {
				valuesDiv.scrollTop(Math.Round(scrollTop + offsetTop + rowHeight - tblHeight));
			}
		}
		
		private void MakeDraggable(jQuery row) {
			row.draggable(new Dictionary("helper", "clone",
				                         "appendTo", element.children("div:eq(1)").children("table").children("tbody"),
				                         "scroll", false,
			                             "containment", "parent",
			                             "start", Utils.Wrap(new UnwrappedDraggableEventHandlerDelegate(delegate(DOMElement d, JQueryEvent evt, DraggableEventObject ui) { JQueryProxy.jQuery(d).addClass(CURRENT_DRAGGING_ROW_CLASS); })),
			                             "stop", Utils.Wrap(new UnwrappedDraggableEventHandlerDelegate(delegate(DOMElement d, JQueryEvent evt, DraggableEventObject ui) { JQueryProxy.jQuery(d).removeClass(CURRENT_DRAGGING_ROW_CLASS); }))
			                        ));
		}
		
		private void AttachToValuesTbody() {
			valuesTbody.children().each(new EachCallback(delegate(int i, DOMElement e) {
				string data = (string)e.GetAttribute("__data");
				Type.SetField(e, "__data", string.IsNullOrEmpty(data) ? null : jQuery.evalJSON(data));
				return true;
			}));
			valuesTbody.children().click(rowClickHandler);
			if (selectedRowIndex >= 0 && enableDragDrop && enabled)
				MakeDraggable(SelectedRow);

			if (enableDragDrop && enabled)
				EnableDroppableRows(valuesTbody.children());
		}
		
		private void EnableDroppableRows(jQuery rows) {
			rows.droppable(new Dictionary("tolerance", "pointer",
			                              "drop", Utils.Wrap(new UnwrappedDroppableEventHandlerDelegate(Row_Drop)),
			                              "greedy", true,
			                              "hoverClass", ROW_HOVER_CLASS
			              ));
		}

		private void EnableDroppableValueDiv() {
			element.children("div:eq(1)").droppable(new Dictionary("tolerance", "pointer", "greedy", true, "drop", new DroppableEventHandlerDelegate(ValuesDiv_Drop)));
		}

		private void Row_Drop(DOMElement targetElem, JQueryEvent evt, DroppableEventObject ui) {
			int newIndex = ((TableRowElement)targetElem).RowIndex;
			newIndex = (newIndex > selectedRowIndex ? newIndex - 1 : newIndex); // If dragging down we have to pretend that the original row does not exist.
			if (newIndex == selectedRowIndex)
				return;

			GridDragDropCompletingEventArgs e = new GridDragDropCompletingEventArgs(selectedRowIndex, newIndex);
			OnDragDropCompleting(e);
			if (e.Cancel)
				return;

			DOMElement draggedElem = ui.draggable.get(0);
			DOMElement valuesTbodyEl = valuesTbody.get(0);
			valuesTbodyEl.RemoveChild(draggedElem);
			valuesTbodyEl.InsertBefore(draggedElem, targetElem);
			selectedRowIndex = newIndex;
			element.focus();

			OnDragDropCompleted(new GridDragDropCompletedEventArgs(selectedRowIndex, newIndex));
		}

		private void ValuesDiv_Drop(JQueryEvent evt, DroppableEventObject ui) {
			if (selectedRowIndex == NumRows - 1)
				return;
			GridDragDropCompletingEventArgs e = new GridDragDropCompletingEventArgs(selectedRowIndex, NumRows - 1);
			OnDragDropCompleting(e);
			if (e.Cancel)
				return;

			DOMElement draggedElem = ui.draggable.get(0);
			DOMElement valuesTbodyEl = valuesTbody.get(0);
			valuesTbodyEl.RemoveChild(draggedElem);
			valuesTbodyEl.AppendChild(draggedElem);
			selectedRowIndex = NumRows - 1;
			element.focus();

			OnDragDropCompleted(new GridDragDropCompletedEventArgs(selectedRowIndex, NumRows - 1));
		}
		
		private void AttachInner() {
			headerTr    = element.children("div:eq(0)").children("table").children("thead").children("tr:first-child");
			valuesTbody = element.children("div:eq(1)").children("table").children("tbody");

			AttachToValuesTbody();
			
			headerTr.children(":not(:last-child)").children().resizable(new Dictionary("handles", "e",
			                                                                           "stop", Utils.Wrap(new UnwrappedResizableEventHandlerDelegate(
			                                                                                   delegate(DOMElement d, JQueryEvent evt, ResizableEventObject ui) {
			                                                                                       int index = headerTr.children().index(d.ParentNode);
			                                                                                       SetColWidth(index, Math.Round(ui.size.width));
			                                                                                   }))
			                                                           ));

			jQuery valuesDiv = element.children("div:eq(1)");
			valuesDiv.height(height - 2 * BORDER_SIZE - (colHeadersVisible ? headerHeight : 0));
			valuesDiv.scroll(delegate {
				element.children("div:eq(0)").scrollLeft(Math.Round(element.children("div:eq(1)").scrollLeft()));
			});
		}
		
		public void Attach() {
			if (id == null || element != null)
				throw new Exception("Must set ID and can only attach once");
		
			element = JQueryProxy.jQuery("#" + id);

			rowClickHandler = (JQueryEventHandlerDelegate)Utils.Wrap(new UnwrappedJQueryEventHandlerDelegate(delegate(DOMElement e, JQueryEvent evt) {
				if (!enabled)
					return;
			
				int rowIndex = ((TableRowElement)e).RowIndex;

				GridCellClickedEventArgs ea = new GridCellClickedEventArgs();
				ea.Row = rowIndex;
				ea.PreventRowSelect = false;
				
				// find the cell which was clicked
				for (DOMElement current = evt.target; current != e; current = current.ParentNode) {
					if (current.TagName.ToLowerCase() == "td") {
						ea.Col = (int)Type.GetField(current, "cellIndex"); // missing property from Script#
						OnCellClicked(ea);
						break;
					}
				}
				
				if (!ea.PreventRowSelect)
					SelectedRowIndex = rowIndex;
			}));

			headerHeight = Math.Round(element.children("div:eq(0)").outerHeight());

			AttachInner();

			if (enableDragDrop && enabled)
				EnableDroppableValueDiv();

			UIUtils.AttachKeyPressHandler(element, el_KeyDown);
            
            if (!colHeadersVisible)
				element.children("div:eq(0)").css("display", "none");
		}

		private void el_KeyDown(JQueryEvent e) {
			if (!enabled)
				return;

			GridKeyPressEventArgs ev = new GridKeyPressEventArgs();
			ev.KeyCode = e.keyCode;
			OnKeyPress(ev);
			if (ev.PreventDefault) {
				e.preventDefault();
				return;
			}

			switch (e.keyCode) {
				case 38:
					// key up
					if (NumRows > 0 && selectedRowIndex  > 0)
						SelectedRowIndex = (selectedRowIndex == -1 ? 0 : SelectedRowIndex - 1);
					e.preventDefault();
					break;

				case 40:
					// key down
					if (NumRows > 0 && selectedRowIndex < NumRows - 1)
						SelectedRowIndex = (SelectedRowIndex == -1 ? 0 : SelectedRowIndex + 1);
					e.preventDefault();
					break;
			}
		}

		private bool RaiseSelectionChanging(int newSelection) {
			GridSelectionChangingEventArgs e = new GridSelectionChangingEventArgs();
			e.OldSelectionIndex = selectedRowIndex;
			e.NewSelectionIndex = newSelection;
			OnSelectionChanging(e);
			return !e.Cancel;
		}

		protected virtual void OnSelectionChanging(GridSelectionChangingEventArgs e) {
			if (SelectionChanging != null)
				SelectionChanging(this, e);
		}
		
		protected virtual void OnSelectionChanged(EventArgs e) {
			if (SelectionChanged != null)
				SelectionChanged(this, e);
		}

		protected virtual void OnCellClicked(GridCellClickedEventArgs e) {
			if (CellClicked != null)
				CellClicked(this, e);
		}
		
		protected virtual void OnKeyPress(GridKeyPressEventArgs e) {
			if (KeyPress != null)
				KeyPress(this, e);
		}
		
		protected virtual void OnDragDropCompleting(GridDragDropCompletingEventArgs e) {
			if (DragDropCompleting != null)
				DragDropCompleting(this, e);
		}

		protected virtual void OnDragDropCompleted(GridDragDropCompletedEventArgs e) {
			if (DragDropCompleted != null)
				DragDropCompleted(this, e);
		}

		public void Focus() {
			if (element != null)
				element.focus();
		}
#endif
	}
}