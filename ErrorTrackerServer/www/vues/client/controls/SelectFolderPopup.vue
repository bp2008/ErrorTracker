<template>
	<div class="selectFolderDialogRoot" ref="dialogRoot" role="dialog" aria-labelledby="textInputMsgTitle" aria-describedby="textInputMsg">
		<div class="titleBar">
			<div id="textInputMsgTitle" class="title" role="alert">Select a Folder</div>
		</div>
		<div class="dialogBody">
			<FolderBrowser ref="folderBrowser" :projectName="projectName" :selectedFolderId="selectedFolderId" @selectedFolderPathChanged="selectedFolderPathChanged" />
		</div>
		<div class="buttons">
			<div class="dialogButton okButton" tabindex="0" @click="okClicked" @keydown.space.enter.prevent="okClicked">
				OK
			</div>
		</div>
	</div>
</template>

<script>
	import FolderBrowser from 'appRoot/vues/client/projectdisplay/folder/FolderBrowser.vue';

	export default {
		components: { FolderBrowser },
		props:
		{
			projectName: {
				type: String,
				default: ""
			}
		},
		data()
		{
			return {
				path: null
			};
		},
		created()
		{
		},
		mounted()
		{
			this.SetFocus();
		},
		computed:
		{
			selectedFolderId()
			{
				return this.$route.query.f ? parseInt(this.$route.query.f) : 0;
			}
		},
		methods:
		{
			SetFocus()
			{
				if (this.$refs.folderBrowser)
					this.$refs.folderBrowser.focusSelectedNode();
			},
			DefaultClose()
			{
				this.okClicked();
			},
			okClicked()
			{
				this.$emit("close", this.path); // path can be null if no folder was selected after creating the popup.
			},
			selectedFolderPathChanged(path)
			{
				this.path = path;
			}
		}
	}
</script>

<style scoped>
	.selectFolderDialogRoot
	{
		max-width: 900px;
		background-color: #FFFFFF;
		display: flex;
		flex-direction: column;
		max-height: 98vh;
	}

	.titleBar
	{
		background-color: #FFFFFF;
		padding: 8px 14px;
		box-sizing: border-box;
	}


	.title
	{
		text-align: center;
		color: black;
		font-weight: bold;
		font-size: 16pt;
	}

	.dialogBody
	{
		padding: 5px;
		flex: 1 1 auto;
		overflow: auto;
	}

	.buttons
	{
		display: flex;
	}

	.dialogButton
	{
		display: inline-block;
		cursor: pointer;
		color: black;
		font-weight: bold;
		font-size: 12pt;
		box-sizing: border-box;
		position: relative;
		padding: 12px 5px;
		flex: 1 0 auto;
		text-align: center;
	}

		.dialogButton:hover
		{
			background-color: rgba(0,0,0,0.05);
		}

	.okButton
	{
		color: #4A9E42;
	}

	.cancelButton
	{
		color: #CE7D29;
		border-left: 1px solid #DDDDDD;
	}

	.filterSummaryList
	{
		max-width: 600px;
		border: 1px solid black;
		border-radius: 5px;
		display: inline-block;
	}

	.filterNode
	{
		display: block;
		padding: 4px 8px;
		font-size: 18px;
		text-decoration: none;
		color: inherit;
		border-bottom: 1px solid #AAAAAA;
	}

		.filterNode:last-child
		{
			border-bottom: none;
		}

	.filterName
	{
	}

	.filterMeta
	{
		margin-left: 15px;
		color: #777777;
	}

		.filterMeta.filterEnabled
		{
			color: #00A000;
		}

		.filterMeta.filterDisabled
		{
			color: #CC0000;
		}
</style>