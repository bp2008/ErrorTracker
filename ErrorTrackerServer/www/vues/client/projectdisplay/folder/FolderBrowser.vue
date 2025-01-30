<template>
	<div class="folderBrowser"
		 @contextmenu.stop.prevent="onContextmenu">
		<div v-if="isSearch" class="searchResultsSection">
			<h3 class="searchingHeading">
				<vsvg sprite="search" role="presentation" class="searchIcon" />
				Search Results
			</h3>
			<template v-if="searchArgs.query">
				<p>for &quot;<b>{{searchArgs.query}}</b>&quot;</p>
			</template>
			<template v-else>
				<p>for <router-link :to="{ name: 'advancedSearch', query: $route.query }">[advanced query]</router-link></p>
			</template>
			<div>in <code class="inline">{{selectedFolderPath}}</code></div>
			<div><vsvg sprite="arrow_right_alt" class="arrowRight" /></div>
			<router-link :to="closeSearchResultsRoute" class="searchResultsClose">Close Search Results</router-link>
		</div>
		<div v-if="error" class="error">
			{{error}}
			<div class="tryAgain"><input type="button" value="Try Again" @click="loadFolders" /></div>
		</div>
		<div v-else-if="filters_error" class="error">
			Failed to load filter list. {{filters_error}}
			<div class="tryAgain"><input type="button" value="Try Again" @click="loadFilters" /></div>
		</div>
		<div v-else-if="rootFolder">
			<FolderNode :projectName="projectName"
						:folder="rootFolder"
						:selectedFolderId="selectedFolderId"
						:isDialog="isDialog"
						:dialogAllowsAllFolders="dialogAllowsAllFolders"
						@menu="onMenu"
						@moveInto="onMoveInto"
						@select="onFolderSelect"
						class="folderRootNode"
						ref="folderNode" />
			<div v-if="loading || filters_loading" class="loadingOverlay">
				<div class="loading"><ScaleLoader /> Updating…</div>
			</div>
		</div>
		<div v-else-if="loading || filters_loading" class="loading"><ScaleLoader /> Loading…</div>
		<div v-else>
			Root folder not found.
			<div class="tryAgain"><input type="button" value="Try Again" @click="loadFolders" /></div>
		</div>
		<!-- Folder Context Menu -->
		<vue-context ref="menu">
			<template slot-scope="folder">
				<li v-if="folder.data && folder.data.FolderId > -1">
					<span class="menuComment">{{folder.data.AbsolutePath}}</span>
				</li>
				<li v-show="folder.data && folder.data.FolderId >= 0">
					<a role="button" @click.prevent="newFolder(folder.data)">New Folder</a>
				</li>
				<li v-show="folder.data && folder.data.FolderId > 0">
					<a role="button" @click.prevent="beginMoveFolder(folder.data)">Move</a>
				</li>
				<li v-show="folder.data && folder.data.FolderId > 0">
					<a role="button" @click.prevent="renameFolder(folder.data)">Rename</a>
				</li>
				<li v-show="folder.data && folder.data.FolderId > 0">
					<a role="button" @click.prevent="deleteFolder(folder.data)">Delete</a>
				</li>
				<li v-show="folder.data && folder.data.FolderId > -1">
					<a role="button" @click.prevent="setFolderReadState(folder.data, true)">Mark Folder Read</a>
				</li>
				<li v-show="folder.data && folder.data.FolderId > -1">
					<a role="button" @click.prevent="setFolderReadState(folder.data, false)">Mark Folder Unread</a>
				</li>
				<li v-show="folder.data" class="v-context__sub">
					<a role="button">Run Filter</a>
					<ul class="v-context">
						<li>
							<a role="button" @click.prevent="runAllEnabledFilters(folder.data)"><b>All Enabled Filters</b></a>
						</li>
						<li v-for="f in filters" :key="f.filter.FilterId" v-if="f.NumActions > 0">
							<a role="button" @click.prevent="runFilter(f.filter, folder.data)">{{f.filter.Name}}</a>
						</li>
					</ul>
				</li>
				<li v-if="nextUndo">
					<a role="button" @click.prevent="undo()">Undo {{nextUndo.description}}</a>
				</li>
				<li v-if="nextRedo">
					<a role="button" @click.prevent="redo()">Redo {{nextRedo.description}}</a>
				</li>
				<li v-if="!nextUndo && !nextRedo && !folder.data">
					<span class="menuComment">no context menu items</span>
				</li>
			</template>
		</vue-context>
	</div>
</template>

<script>
	import { GetFolderStructure, AddFolder, RenameFolder, MoveFolder, DeleteFolder, RunFilterOnFolder, RunEnabledFiltersOnFolder } from 'appRoot/api/FolderData';
	import { MoveEvents, CountUnreadEventsByFolder, SetFolderReadState } from 'appRoot/api/EventData';
	import { GetAllFilters } from 'appRoot/api/FilterData';
	import { VueContext } from 'vue-context';
	import { TextInputDialog, ModalConfirmDialog } from 'appRoot/scripts/ModalDialog';
	import EventBus from 'appRoot/scripts/EventBus';
	import { ParseDraggingItems } from 'appRoot/scripts/Util';
	import svg1 from 'appRoot/images/sprite/arrow_right_alt.svg';
	import svg2 from 'appRoot/images/sprite/search.svg';

	export default {
		components: { VueContext },
		props:
		{
			projectName: { // pre-validated
				type: String,
				required: true
			},
			selectedFolderId: {
				type: Number,
				default: 0
			},
			searchArgs: null,
			isDialog: {
				type: Boolean,
				default: false
			},
			dialogAllowsAllFolders: {
				type: Boolean,
				default: false
			}
		},
		data()
		{
			return {
				error: null,
				loading: false,
				rootFolder: null,
				filters_error: null,
				filters_loading: false,
				filters: null,
				focusSelectedNodeWhenLoaded: false
			};
		},
		created()
		{
			EventBus.$on("eventReadStateChanged", this.eventReadStateChanged);
			EventBus.$on("eventMoved", this.eventMoved);
			EventBus.$on("eventDeleted", this.eventDeleted);
			EventBus.$on("folderReadStateChanged", this.folderReadStateChanged);
			this.loadFilters();
			this.loadFolders();
		},
		beforeDestroy()
		{
			EventBus.$off("eventReadStateChanged", this.eventReadStateChanged);
			EventBus.$off("eventMoved", this.eventMoved);
			EventBus.$off("eventDeleted", this.eventDeleted);
			EventBus.$off("folderReadStateChanged", this.folderReadStateChanged);
			EventBus.stopMovingItem();
		},
		computed:
		{
			isSearch()
			{
				return this.searchArgs && !!(this.searchArgs.query || this.searchArgs.conditions);
			},
			selectedFolderPath()
			{
				return this.getFolderPath(this.selectedFolderId);
			},
			closeSearchResultsRoute()
			{
				let query = Object.assign({}, this.$route.query);
				delete query.q;
				delete query.matchAll;
				delete query.scon;
				return { name: this.$route.name, query };
			},
			externalChangesToFolders()
			{
				return EventBus.externalChangesToFolders;
			},
			nextUndo()
			{
				return EventBus.NextUndoOperation(this.projectName);
			},
			nextRedo()
			{
				return EventBus.NextRedoOperation(this.projectName);
			},
			folderIdMap()
			{
				let map = {};
				BuildFolderIdMap(map, this.rootFolder);
				return map;
			}
		},
		methods:
		{
			loadFolders()
			{
				this.loading = true;
				this.error = null;
				EventBus.stopMovingItem();

				GetFolderStructure(this.projectName)
					.then(data =>
					{
						if (data.success)
						{
							if (!this.isDialog || this.dialogAllowsAllFolders)
								data.root.Children.push({ FolderId: -1, Name: "All Folders", AbsolutePath: "🗄️ All Folders" });
							predefineFolderUnreadEventCounts(data.root);
							EventBus.learnProjectFolderStructure(this.projectName, data.root);
							this.rootFolder = data.root;
							if (this.focusSelectedNodeWhenLoaded)
							{
								this.focusSelectedNodeWhenLoaded = false;
								this.$nextTick(() =>
								{
									this.focusSelectedNode();
								});
							}
							this.loadUnreadEventCounts();
						}
						else
						{
							this.error = data.error;
							this.rootFolder = null;
						}
					})
					.catch(err =>
					{
						this.error = err.message;
						this.rootFolder = null;
					})
					.finally(() =>
					{
						this.loading = false;
					});
			},
			loadUnreadEventCounts()
			{
				CountUnreadEventsByFolder(this.projectName)
					.then(data =>
					{
						if (data.success)
						{
							if (this.rootFolder)
								setFolderUnreadEventCounts(this.rootFolder, data.folderIdToUnreadEventCount);
						}
						else
							toaster.warning(data.error);
					})
					.catch(err =>
					{
						toaster.warning(err.message);
					});
			},
			newFolder(folder)
			{
				TextInputDialog("Name new folder", "Enter a name for the new folder.", "Name")
					.then(result =>
					{
						if (result)
						{
							this.handleAsyncFolderOp(AddFolder(this.projectName, result.value, folder.FolderId));
						}
					});
			},
			beginMoveFolder(folder)
			{
				EventBus.movingItem = "f" + folder.FolderId;
				EventBus.tooltipHtml = 'Carrying folder <b>' + folder.Name + '</b>.\n\nClick another folder to drop it into.';
			},
			renameFolder(folder)
			{
				TextInputDialog("Rename", 'Rename "' + folder.Name + '"', "New Name", folder.Name)
					.then(result =>
					{
						if (result)
						{
							this.handleAsyncFolderOp(RenameFolder(this.projectName, folder.FolderId, result.value));
						}
					});
			},
			deleteFolder(folder)
			{
				ModalConfirmDialog('Are you sure you want to delete the folder "' + folder.Name + '"?', "Confirm Deletion")
					.then(result =>
					{
						if (result)
						{
							this.handleAsyncFolderOp(DeleteFolder(this.projectName, folder.FolderId));
						}
					});
			},
			setFolderReadState(folder, read)
			{
				this.loading = true;
				SetFolderReadState(this.projectName, folder.FolderId, read)
					.then(data =>
					{
						if (data.success)
						{
							EventBus.$emit("folderReadStateChanged", { FolderId: folder.FolderId, Read: read, UnreadEventCount: data.unreadEventCount });
							this.loading = false;
						}
						else
						{
							toaster.error(data.error);
							this.loading = false;
						}
					})
					.catch(err =>
					{
						toaster.error(err);
						this.loading = false;
					});
			},
			moveFolder(folderId, newParentFolderId)
			{
				if (folderId === newParentFolderId)
				{
					toaster.warning("Cannot move folder into itself.");
					return;
				}

				this.handleAsyncFolderOp(MoveFolder(this.projectName, folderId, newParentFolderId),
					() =>
					{
						let folder = getFolderRecursive(this.rootFolder, folderId);
						let from = getParentOfFolder(this.rootFolder, folderId);
						let to = getFolderRecursive(this.rootFolder, newParentFolderId);
						if (folder && from && to)
						{
							EventBus.NotifyPerformedUndoableOperation(this.projectName, {
								type: "MoveFolder",
								description: 'Move Folder "' + folder.Name + '" to "' + to.Name + '"',
								folderId: folderId,
								from: from.FolderId,
								to: to.FolderId
							});
						}
					});
			},
			onMenu({ e, folder })
			{
				if (EventBus.movingItem)
					EventBus.stopMovingItem();
				else
					this.$refs.menu.open(e, folder);
			},
			onContextmenu(e)
			{
				this.onMenu({ e: e, folder: null });
			},
			handleAsyncFolderOp(promise, onSuccess)
			{
				this.loading = true;
				promise
					.then(data =>
					{
						if (data.success)
						{
							if (typeof onSuccess === "function")
								onSuccess();
							this.loadFolders();
						}
						else
						{
							toaster.error(data.error);
							this.loading = false;
						}
					})
					.catch(err =>
					{
						toaster.error(err);
						this.loading = false;
					})
			},
			onMoveInto(args)
			{
				if (!args.target)
				{
					toaster.error("received invalid onMoveInto event from a folder");
					return; // Unknown target;
				}
				if (args.source)
				{
					// This is an HTML5 drag and drop operation. The source could be folders or events.
					let eventIds = [];
					for (let i = 0; i < args.source.length; i++)
					{
						let item = args.source[i];
						if (item.type === "f")
							this.moveFolder(item.id, args.target.FolderId);
						else if (item.type === "e")
							eventIds.push(item.id);
						else
							toaster.error("FolderBrowser does not implement a move operation for item of type \"" + item.type + "\"");
					}
					if (eventIds.length > 0)
						this.moveEvents(eventIds, args.target.FolderId);
				}
				else
				{
					// This must be a dragless move operation triggered by context menu.
					args.source = ParseDraggingItems(EventBus.movingItem);
					if (args.source)
						this.onMoveInto(args);
					else
						toaster.error("Application Error. Unable to complete move operation.");
					EventBus.stopMovingItem();
				}
			},
			moveEvents(eventIds, newFolderId)
			{
				MoveEvents(this.projectName, eventIds, newFolderId)
					.then(data =>
					{
						if (data.success)
						{
							let folderPath = this.getFolderPath(newFolderId);
							if (folderPath == null)
								folderPath = "unknown folder";
							let description = eventIds.length + " event" + (eventIds.length == 1 ? "" : "s") + " to " + folderPath;

							let moves = [];
							for (let i = 0; i < eventIds.length; i++)
							{
								let eventSummary = EventBus.getEventSummary(eventIds[i]);
								let oldFolderId;
								if (eventSummary)
									oldFolderId = eventSummary.FolderId;
								else
								{
									oldFolderId = this.selectedFolderId;
									console.error("Unable to locate cached EventSummary for EventId " + eventIds[i]);
								}
								if (eventSummary)
								{
									EventBus.$emit("eventMoved", {
										event: eventSummary,
										from: oldFolderId,
										to: newFolderId
									});
								}
								else
									console.log("Unable to emit eventMoved event because the event summary is not cached for event ID " + eventIds[i]);
								moves.push({ eventId: eventIds[i], from: oldFolderId, to: newFolderId });
							}
							// Push onto undo stack
							EventBus.NotifyPerformedUndoableOperation(this.projectName, {
								type: "MoveEvents",
								description: "Move " + description,
								moves: moves
							});

							toaster.success("Moved " + description);
							EventBus.externalChangesToVisibleEvents++;
							this.loadFolders();
						}
						else
						{
							toaster.error(data.error);
							EventBus.externalChangesToVisibleEvents++;
						}
					})
					.catch(err =>
					{
						toaster.error(err);
					});
			},
			loadFilters()
			{
				this.filters_error = null;
				this.filters_loading = true;
				this.filters = null;

				GetAllFilters(this.projectName)
					.then(data =>
					{
						if (data.success)
							this.filters = data.filters;
						else
							this.filters_error = data.error;
					})
					.catch(err =>
					{
						this.filters_error = err.message;
					})
					.finally(() =>
					{
						this.filters_loading = false;
					});
			},
			runFilter(filter, folder)
			{
				this.loading = true;
				RunFilterOnFolder(this.projectName, filter.FilterId, folder.FolderId)
					.then(data =>
					{
						this.loading = false;
						if (data.success)
						{
							toaster.success("Filter execution completed");
							EventBus.externalChangesToVisibleEvents++;
							this.loadFolders();
						}
						else
							toaster.error(data.error);
					})
					.catch(err =>
					{
						this.loading = false;
						toaster.error(err);
					});
			},
			runAllEnabledFilters(folder)
			{
				this.loading = true;
				RunEnabledFiltersOnFolder(this.projectName, folder.FolderId)
					.then(data =>
					{
						this.loading = false;
						if (data.success)
						{
							toaster.success("Filter execution completed");
							EventBus.externalChangesToVisibleEvents++;
							this.loadFolders();
						}
						else
							toaster.error(data.error);
					})
					.catch(err =>
					{
						this.loading = false;
						toaster.error(err);
					});
			},
			getFolder(folderId)
			{
				return this.folderIdMap[folderId];
			},
			getFolderPath(folderId)
			{
				return EventBus.getProjectFolderPathFromId(this.projectName, folderId);
				//let folder = getFolderRecursive(this.rootFolder, folderId);
				//return folder ? folder.AbsolutePath : null;
			},
			focusSelectedNode()
			{
				if (this.$refs.folderNode)
					this.$refs.folderNode.focusSelectedNode();
				else
					this.focusSelectedNodeWhenLoaded = true;
			},
			eventReadStateChanged(event)
			{
				let folder = getFolderRecursive(this.rootFolder, event.FolderId);
				if (folder)
				{
					if (event.Read)
						folder.Unread--;
					else
						folder.Unread++;
				}
			},
			folderReadStateChanged({ FolderId, Read, UnreadEventCount })
			{
				let folder = getFolderRecursive(this.rootFolder, FolderId);
				if (folder)
				{
					folder.Unread = UnreadEventCount;
				}
			},
			eventMoved({ event, from, to })
			{
				if (event && !event.Read)
				{
					let fromFolder = this.getFolder(from);
					let toFolder = this.getFolder(to);
					if (fromFolder)
						fromFolder.Unread--;
					if (toFolder)
						toFolder.Unread++;
				}
			},
			eventDeleted(event)
			{
				if (event && !event.Read)
				{
					let folder = this.getFolder(event.FolderId);
					if (folder)
						folder.Unread--;
				}
			},
			onFolderSelect(folder)
			{
				this.$emit('select', folder);
			},
			undo()
			{
				EventBus.PerformUndo(this.projectName);
			},
			redo()
			{
				EventBus.PerformRedo(this.projectName);
			}
		},
		watch:
		{
			projectName()
			{
				this.loadFilters();
				this.loadFolders();
			},
			externalChangesToFolders()
			{
				this.loadFolders();
			}
		}
	}
	function getParentOfFolder(root, folderId)
	{
		if (root)
		{
			if (root.Children && root.Children.length)
				for (let i = 0; i < root.Children.length; i++)
				{
					if (root.Children[i].FolderId === folderId)
						return root;
					let found = getParentOfFolder(root.Children[i], folderId);
					if (found)
						return found;
				}
		}
		return null;
	}
	function getFolderRecursive(root, folderId)
	{
		if (root)
		{
			if (root.FolderId === folderId)
				return root;
			if (root.Children && root.Children.length)
				for (let i = 0; i < root.Children.length; i++)
				{
					let found = getFolderRecursive(root.Children[i], folderId);
					if (found)
						return found;
				}
		}
		return null;
	}
	function BuildFolderIdMap(map, root)
	{
		if (root)
		{
			map[root.FolderId] = root;
			if (root.Children && root.Children.length)
				for (let i = 0; i < root.Children.length; i++)
					BuildFolderIdMap(map, root.Children[i]);
		}
	}
	function predefineFolderUnreadEventCounts(folder)
	{
		folder.Unread = 0;
		if (folder.Children && folder.Children.length)
			for (let i = 0; i < folder.Children.length; i++)
				predefineFolderUnreadEventCounts(folder.Children[i]);
	}
	function setFolderUnreadEventCounts(folder, folderIdToUnreadEventCount)
	{
		let unread = folderIdToUnreadEventCount[folder.FolderId];
		if (unread)
			folder.Unread = unread;

		if (folder.Children && folder.Children.length)
			for (let i = 0; i < folder.Children.length; i++)
				setFolderUnreadEventCounts(folder.Children[i], folderIdToUnreadEventCount);
	}
</script>

<style scoped>
	.folderBrowser
	{
		position: relative;
		width: 100%;
		height: 100%;
	}

	.folderRootNode
	{
		padding: 4px 8px;
	}

	.loading
	{
		margin-top: 80px;
		text-align: center;
	}

	.loadingOverlay
	{
		position: absolute;
		top: 0px;
		left: 0px;
		width: 100%;
		height: 100%;
		background-color: rgba(0,0,0,0.25);
		z-index: 10;
		user-select: none;
		box-sizing: border-box;
		word-break: break-word;
	}

	.loadingOverlay
	{
		display: flex;
		align-items: center;
		justify-content: center;
	}

		.loadingOverlay .loading
		{
			margin-top: 0px;
			background-color: rgba(255,255,255,0.95);
			padding: 4px;
			border-radius: 4px;
		}

	.searchResultsSection
	{
		padding: 10px;
		padding-bottom: 20px;
		background-color: #e0eff3;
		text-align: center;
		border-bottom: 1px solid #000000;
		margin-bottom: 10px;
	}


	.searchResultsClose
	{
		display: inline-block;
		cursor: pointer;
		padding: 10px;
		border: 1px solid #909090;
		background-color: #F5F7F7;
		color: inherit;
		text-decoration: none;
	}

		.searchResultsClose:hover
		{
			background-color: #FFFFFF;
		}

	.arrowRight
	{
		width: 67px;
		height: 67px;
	}

	.error
	{
		color: #FF0000;
		font-weight: bold;
		padding: 20px 5px;
		text-align: center;
	}

	.tryAgain
	{
		margin-top: 10px;
	}

	.menuComment
	{
		display: block;
		padding: .5rem 1.5rem;
		font-weight: 400;
		color: #999999;
		font-style: italic;
		text-decoration: none;
		white-space: nowrap;
		background-color: transparent;
		border: 0;
	}

	.searchingHeading
	{
		display: flex;
		align-items: center;
		justify-content: center;
	}

	.searchIcon
	{
		width: 30px;
		height: 30px;
		padding-right: 5px;
	}
</style>