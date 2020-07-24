<template>
	<div class="folderNode">
		<div class="nodeHead" @contextmenu.prevent="onContextmenu">
			<vsvg v-if="folder.Children && folder.Children.length" sprite="expand_more" :class="{ nodeIcon: true, visible: true, collapsed: !expanded }"
				  tabindex="0" @click="toggleExpansion" @keypress.enter.prevent="toggleExpansion" />
			<div v-else class="nodeIcon"></div>
			<FolderLink :to="openFolderRoute"
						:clickFn="folderLinkClick"
						:class="{ link: true, selected: selected, moving: moving, dragTarget: isDragTarget }"
						:draggable="movingItem ? 'false' : 'true'"
						@dragstart.native="dragStart"
						@dragover.native="dragOver"
						@dragenter.native="dragEnter"
						@dragleave.native="dragLeave"
						@drop.native="onDrop"
						:title="linkTitle"
						ref="myNode">
				<span class="folderIcon" v-if="isAllFolders">🗄️</span>
				<span class="folderIcon" v-else-if="selected">📂</span>
				<span class="folderIcon" v-else>📁</span>
				<span class="name">{{ folder.Name }}</span>
				<span class="unread" v-if="folder.Unread">({{folder.Unread}})</span>
			</FolderLink>
		</div>
		<div class="children" v-show="expanded">
			<FolderNode v-for="child in folder.Children"
						:key="'folder' + child.FolderId"
						:projectName="projectName"
						:folder="child"
						:selectedFolderId="selectedFolderId"
						:isDialog="isDialog"
						:dialogAllowsAllFolders="dialogAllowsAllFolders"
						@select="onSelect"
						@menu="onMenu"
						@moveInto="onMoveInto"
						ref="childNodes" />
		</div>
	</div>
</template>

<script>
	import svg1 from 'appRoot/images/sprite/expand_more.svg';
	import FolderLink from 'appRoot/vues/client/projectdisplay/folder/FolderLink.vue';
	import EventBus from 'appRoot/scripts/EventBus';
	import { ParseDraggingItems } from 'appRoot/scripts/Util';

	export default {
		components: { FolderLink },
		props:
		{
			projectName: {
				type: String,
				required: true
			},
			folder: {
				type: Object,
				required: true
			},
			selectedFolderId: {
				type: Number,
				default: 0
			},
			isDialog: { // Prevents clicking a folder from causing navigation or completing a dragless event move operation.
				type: Boolean, // If true, clicking a folder will emit the "select" event.
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
				isDragTarget: false
			};
		},
		computed:
		{
			nodeSprite()
			{
				return this.expanded ? "folder_open" : "folder";
			},
			selected()
			{
				return this.folder.FolderId === this.selectedFolderId;
			},
			moving()
			{
				return "f" + this.folder.FolderId === EventBus.movingItem;
			},
			openFolderRoute()
			{
				if (EventBus.movingItem || this.isDialog)
					return null;
				let query = Object.assign({}, this.$route.query);
				query.f = this.folder.FolderId;
				return { name: this.$route.name, query, params: this.$route.params };
			},
			expanded()
			{
				return !this.$store.getters.isFolderCollapsed(this.projectName, this.folder.FolderId);
			},
			movingItem()
			{
				return EventBus.movingItem;
			},
			isAllFolders()
			{
				return this.folder.FolderId === -1;
			},
			linkTitle()
			{
				if (this.isAllFolders)
					return "A special node you can select to view or search the contents of all folders in the project.";
				else
					return null;
			}
		},
		methods:
		{
			toggleExpansion()
			{
				this.$store.commit("SetFolderCollapsedState", { projectName: this.projectName, folderId: this.folder.FolderId, collapsed: this.expanded });
			},
			onContextmenu(e)
			{
				this.$emit("menu", { e, folder: this.folder });
			},
			onMenu(args)
			{
				this.$emit("menu", args);
			},
			folderLinkClick()
			{
				// called on link click only if openFolderRoute returned falsy
				if (this.isDialog)
					this.$emit('select', this.folder);
				if (this.isAllFolders)
					return;
				this.$emit('moveInto', { target: this.folder });
			},
			onSelect(folder)
			{
				this.$emit('select', folder);
			},
			dragStart(e)
			{
				if (this.isAllFolders)
					return;
				e.dataTransfer.setData("etrk_drag", "f" + this.folder.FolderId);
				e.dataTransfer.dropEffect = "move";
			},
			dragOver(e)
			{
				if (this.isAllFolders)
					return;
				if (e.dataTransfer.types.indexOf("etrk_drag") > -1)
				{
					e.dataTransfer.dropEffect = "move";
					e.preventDefault();
				}
			},
			onDrop(e)
			{
				if (this.isAllFolders)
					return;
				this.isDragTarget = false;
				let items = GetDraggingItems(e);
				if (items.length)
				{
					e.preventDefault();
					this.$emit('moveInto', { source: items, target: this.folder });
				}
			},
			dragEnter(e)
			{
				if (this.isAllFolders)
					return;
				this.isDragTarget = true;
			},
			dragLeave(e)
			{
				if (this.isAllFolders)
					return;
				this.isDragTarget = false;
			},
			onMoveInto(args)
			{
				if (this.isAllFolders)
					return;
				this.$emit('moveInto', args);
			},
			focus()
			{
				if (this.$refs.myNode)
					this.$refs.myNode.$el.focus();
			},
			focusSelectedNode()
			{
				if (this.selected)
				{
					this.focus();
					return true;
				}
				else if (this.$refs.childNodes && this.$refs.childNodes.length)
				{
					for (let i = 0; i < this.$refs.childNodes.length; i++)
						if (this.$refs.childNodes[i].focusSelectedNode())
							return true;
				}
				return false;
			}
		}
	}
	/**
	 * Given an HTML5 drag and drop event, gets an array of recognized transfer items.
	 * Example return value: []
	 * Example return value: [{ type: 'f', id: 8 }]
	 * Example return value: [{ type: 'e', id: 1 }, { type: 'e', id: 2 }, { type: 'e', id: 5 }]
	 * @param e HTML5 drag and drop event
	 */
	function GetDraggingItems(e)
	{
		return ParseDraggingItems(e.dataTransfer.getData("etrk_drag"));
	}
</script>

<style scoped>
	.nodeHead
	{
		display: flex;
		align-items: center;
	}

	.link
	{
		font-size: 16px;
		user-select: none;
		cursor: default;
		text-decoration: none;
		color: inherit;
		flex: 1 1 auto;
	}

		.link:hover
		{
			background-color: #E5F3FF;
		}

		.link.selected
		{
			background-color: #CDE8FF;
			outline: 1px solid #7BC3FF;
			z-index: 1;
		}

		.link:focus
		{
			outline: 2px solid #7BC3FF;
			z-index: 1;
		}

		.link.moving
		{
			background-color: #FFE8CD;
			outline: 1px solid #FFC37B;
			z-index: 2;
		}

		.link.dragTarget
		{
			background-color: #7AB4EE;
		}

	.name
	{
		padding: 1px 2px;
		flex: 1 1 auto;
	}

	.unread
	{
		font-weight: bold;
	}

	.nodeIcon
	{
		width: 20px;
		height: 20px;
	}

		.nodeIcon.visible:hover
		{
			background-color: #E5F3FF;
		}

	.folderIcon
	{
		margin-right: 1px;
	}

	.nodeIcon
	{
		display: inline-block;
		flex: 0 0 auto;
	}

		.nodeIcon.collapsed
		{
			transform: rotate(-90deg);
		}

		.nodeIcon:focus
		{
			outline: 1px solid #7BC3FF;
		}

	.children
	{
		margin-left: 8px;
	}
</style>