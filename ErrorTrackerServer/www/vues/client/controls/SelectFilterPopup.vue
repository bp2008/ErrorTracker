<template>
	<div class="selectFilterDialogRoot" ref="dialogRoot" role="dialog" aria-labelledby="textInputMsgTitle" aria-describedby="textInputMsg">
		<div class="titleBar">
			<div id="textInputMsgTitle" class="title" role="alert">Select a Filter</div>
		</div>
		<div class="dialogBody">
			<div class="inputWrapper">
				<div v-if="error" class="error">
					{{error}}
					<div class="tryAgain"><input type="button" value="Try Again" @click="loadFilterSummaries" /></div>
				</div>
				<div v-else-if="loading || filterSummaries">
					<div class="filterSummaryList" v-if="filterSummaries && filterSummaries.length">
						<a role="button" tabindex="0"
						   v-for="f in filterSummaries"
						   :key="f.filter.FilterId"
						   class="filterNode"
						   @click="selectFilter(f.filter)"
						   ref="filterNodes">
							<span class="filterName" v-text="f.filter.Name" />
							<span class="filterMeta">[{{f.NumConditions}} Conditions, {{f.NumActions}} Actions]</span>
							<span class="filterMeta filterEnabled" v-if="f.filter.Enabled">[Enabled]</span>
							<span class="filterMeta filterDisabled" v-if="!f.filter.Enabled">[Disabled]</span>
						</a>
					</div>
					<div v-if="filterSummaries && filterSummaries.length === 0">This project does not have any filters yet.</div>
					<div v-if="loading" class="loadingOverlay">
						<div class="loading"><ScaleLoader /> Loading…</div>
					</div>
				</div>
				<div v-else>
					Failed to load filter list for project: {{projectName}}
					<div class="tryAgain"><input type="button" value="Try Again" @click="loadFilterSummaries" /></div>
				</div>
			</div>
		</div>
		<div class="buttons">
			<div class="dialogButton cancelButton" tabindex="0" @click="cancelClicked" @keydown.space.enter.prevent="cancelClicked">
				Cancel
			</div>
		</div>
	</div>
</template>

<script>
	import { GetAllFilters } from 'appRoot/api/FilterData';

	export default {
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
				error: null,
				loading: false,
				filterSummaries: null
			};
		},
		created()
		{
			this.loadFilterSummaries();
		},
		mounted()
		{
			this.SetFocus();
		},
		methods:
		{
			SetFocus()
			{
			},
			DefaultClose()
			{
				this.cancelClicked();
			},
			cancelClicked()
			{
				this.$emit("close");
			},
			selectFilter(filter)
			{
				this.$emit("close", filter);
			},
			loadFilterSummaries()
			{
				this.loading = true;
				this.error = null;

				GetAllFilters(this.projectName)
					.then(data =>
					{
						if (data.success)
						{
							this.filterSummaries = data.filters;
							this.$nextTick(() =>
							{
								if (this.$refs.filterNodes && this.$refs.filterNodes.length)
									this.$refs.filterNodes[0].focus();
							});
						}
						else
							this.error = data.error;
					})
					.catch(err =>
					{
						this.error = err.message;
					})
					.finally(() =>
					{
						this.loading = false;
					});
			}
		}
	}
</script>

<style scoped>
	.selectFilterDialogRoot
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