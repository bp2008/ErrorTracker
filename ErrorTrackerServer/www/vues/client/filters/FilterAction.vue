<template>
	<div :class="{ action: true, disabled: !action.Enabled }">
		<div class="topRow">
			<label title="Disabled actions are ignored when the filter succeeds"><input type="checkbox" v-model="action.Enabled" /> Enabled</label>
			<input type="button" value="Delete" @click="$emit('delete', action)" />
		</div>
		<div>
			<select v-model="action.Operator">
				<option value="MoveTo">move event to</option>
				<option value="Delete">delete event</option>
				<option value="SetColor">set event color</option>
				<option value="StopExecution">stop execution against matched event</option>
				<option value="MarkRead">mark read</option>
				<option value="MarkUnread">mark unread</option>
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
	import { GetReadableTextColorHexForBackgroundColorHex } from 'appRoot/scripts/Util';
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
			}
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