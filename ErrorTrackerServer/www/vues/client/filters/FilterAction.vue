<template>
	<div :class="{ action: true, disabled: !action.Enabled, searchMatched }">
		<div class="topRow">
			<label title="Disabled actions are ignored when the filter succeeds"><input type="checkbox" v-model="action.Enabled" /> Enabled</label>
			<input type="button" value="Delete" @click="$emit('delete', action)" :title="deleteBtnTooltip" />
		</div>
		<div>
			<select v-model="action.Operator">
				<option v-for="option in operatorOptions" :value="option.key">{{option.value}}</option>
			</select>
		</div>
		<div v-if="action.Operator === 'MoveTo'" class="moveToInputs">
			<input type="text"
				   v-model="action.Argument"
				   class="argumentTextInput" />
			<input type="button" value="Browse" @click="browseFolders" />
		</div>
		<div v-else-if="action.Operator === 'SetColor'"
			 class="argumentColor">
			<input type="color" v-model="action.Argument" placeholder="hex color (e.g. #EBEBEB)" />
			<span :style="colorSampleStyle" class="colorSample">Sample</span>
		</div>
	</div>
</template>

<script>
	import { GetReadableTextColorHexForBackgroundColorHex, FilterMatch } from 'appRoot/scripts/Util';
	import { SelectFolderDialog } from 'appRoot/scripts/ModalDialog';

	export default {
		props:
		{
			action: {
				type: Object,
				required: true
			},
			projectName: {
				type: String,
				required: true
			},
			searchQuery: {
				type: String,
				default: ""
			},
			regexSearch: {
				type: Boolean,
				default: false
			}
		},
		data()
		{
			return {
				operatorOptions: [{ key: "MoveTo", value: "move event to" },
				{ key: "Delete", value: "delete event" },
				{ key: "SetColor", value: "set event color" },
				{ key: "StopExecution", value: "stop execution against matched event" },
				{ key: "MarkRead", value: "mark read" },
				{ key: "MarkUnread", value: "mark unread" }]
			};
		},
		computed:
		{
			colorSampleStyle()
			{
				if (this.action.Operator === "SetColor")
				{
					let color = this.action.Argument;
					if (!color)
						color = "FFFFFF";
					if (color.startsWith("#"))
						color = color.substr(1);
					let s = {
						backgroundColor: "#" + color,
						color: "#" + GetReadableTextColorHexForBackgroundColorHex(color, "000000", "FFFFFF")
					};

					console.log("colorSampleStyle", s);
					return s;
				}
				return null;
			},
			deleteBtnTooltip()
			{
				return 'Delete Action' + (this.action.FilterActionId > 0 ? ' ' + this.action.FilterActionId : '');
			},
			searchMatched()
			{
				if (this.searchQuery)
				{
					for (let i = 0; i < this.operatorOptions.length; i++)
						if (this.operatorOptions[i].key === this.action.Operator && FilterMatch(this.operatorOptions[i].value, this.searchQuery, this.regexSearch))
							return true;
					return FilterMatch(this.action.Argument, this.searchQuery, this.regexSearch);
				}
				return false;
			}
		},
		methods:
		{
			browseFolders()
			{
				SelectFolderDialog(this.projectName, 0, false)
					.then(folder =>
					{
						if (folder)
							this.action.Argument = folder.AbsolutePath;
					});
			}
		}
	}
</script>

<style scoped>
	.action.disabled
	{
		background-color: #FFCCCC;
	}

		.action.disabled:nth-child(2n)
		{
			background-color: #DDBBBB !important;
		}

	.action.searchMatched
	{
		background-color: #FFFF00 !important;
	}

	.topRow
	{
		display: flex;
		justify-content: space-between;
		margin-bottom: 5px;
	}

	.moveToInputs
	{
		display: flex;
		align-items: baseline;
	}

	.argumentTextInput
	{
		margin-top: 5px;
		margin-right: 5px;
		flex: 1 1 auto;
		width: 100%;
		box-sizing: border-box;
	}

	.argumentColor
	{
		margin-top: 5px;
		display: flex;
		align-items: stretch;
	}

	.colorSample
	{
		margin-left: 10px;
		padding: 4px 8px;
		background-color: #FFFFFF;
		font-weight: bold;
	}
</style>