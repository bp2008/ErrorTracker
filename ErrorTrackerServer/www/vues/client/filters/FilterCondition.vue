<template>
	<div :class="{ condition: true, disabled: !condition.Enabled }">
		<div class="topRow">
			<label title="Disabled conditions are ignored when the filter runs. It will be as if they do not exist."><input type="checkbox" v-model="condition.Enabled" /> Enabled</label>
			<input type="button" value="Delete" @click="$emit('delete', condition)" :title="deleteBtnTooltip" />
		</div>
		<div>
			<input type="text" v-model.lazy="condition.TagKey" placeholder="Field Name" title="Field Name (case-insensitive)" />
			<label title="Invert (negate result)"><input type="checkbox" v-model="condition.Invert" />!</label>
			<select v-model="condition.Operator">
				<option value="Contains">contains</option>
				<option value="Equals">equals</option>
				<option value="StartsWith">starts with</option>
				<option value="EndsWith">ends with</option>
			</select>
			<label title="If checked, the Query is a regular expression"><input type="checkbox" v-model="condition.Regex" /> Regex</label>
		</div>
		<input type="text" v-model.lazy="condition.Query" placeholder="Query" title="Query (case-insensitive)" class="queryInput" />
	</div>
</template>

<script>
	export default {
		props:
		{
			condition: {
				type: Object,
				required: true
			}
		},
		computed:
		{
			deleteBtnTooltip()
			{
				return 'Delete Condition' + (this.condition.FilterConditionId > 0 ? ' ' + this.condition.FilterConditionId : '');
			}
		}
	}
</script>

<style scoped>
	.condition.disabled
	{
		background-color: #FFCCCC !important;
	}

		.condition.disabled:nth-child(2n)
		{
			background-color: #DDBBBB !important;
		}

	.topRow
	{
		display: flex;
		justify-content: space-between;
		margin-bottom: 5px;
	}

	.queryInput
	{
		margin-top: 5px;
		width: 100%;
		box-sizing: border-box;
	}
</style>