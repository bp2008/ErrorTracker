<template>
	<div class="advancedSearchHome">
		<div class="inputSection conditions">
			<label class="inputLabel">Advanced Search Conditions</label>
			<div class="conditionList">
				<FilterCondition v-for="condition in internal_conditions"
								 :key="condition.FilterConditionId"
								 :condition="condition"
								 @delete="deleteCondition"
								 class="condition" />
			</div>
			<div>
				<input type="button" value="New Condition" @click="newCondition" />
			</div>
		</div>
		<div class="buttonBar">
			<router-link :to="searchRoute" class="execSearchButton">
				<vsvg sprite="search" role="presentation" class="searchIcon" />
				Execute Search
			</router-link>
		</div>
		<div class="notes">
			<div class="notesHeading">Advanced Search Notes</div>
			<div class="notesBody">
				<ul>
					<li>In <b>conditions</b>, the <code class="inline">Field Name</code> and <code class="inline">Query</code> fields are case-insensitive.</li>
					<li>A <b>condition's</b> <code class="inline">Field Name</code> field can be <code class="inline">EventType</code>, <code class="inline">SubType</code>, <code class="inline">Message</code>, <code class="inline">Date</code>, <code class="inline">Folder</code>, <code class="inline">Color</code>, or any application-specific tag key. An empty <code class="inline">Field Name</code> will fail the condition.</li>
				</ul>
			</div>
		</div>
	</div>
</template>

<script>
	import FilterCondition from 'appRoot/vues/client/filters/FilterCondition.vue';
	import { CopyArray } from 'appRoot/scripts/Util';
	import { ModalConfirmDialog } from 'appRoot/scripts/ModalDialog';
	import svg1 from 'appRoot/images/sprite/search.svg';

	export default {
		components: { FilterCondition },
		props:
		{
			projectName: {
				type: String,
				default: ""
			},
			filterId: {
				type: String,
				default: ""
			},
			searchArgs: null
		},
		data()
		{
			return {
				internal_conditions: []
			};
		},
		created()
		{
			this.loadConditionsFromProp();
		},
		computed:
		{
			conditionsFromProp()
			{
				return this.searchArgs && this.searchArgs.conditions ? this.searchArgs.conditions : null;
			},
			searchRoute()
			{
				return {
					name: "clientHome",
					query: this.$route.query
				};
			}
		},
		methods:
		{
			loadConditionsFromProp()
			{
				if (this.conditionsFromProp && this.conditionsFromProp.length)
					this.internal_conditions = CopyArray(this.conditionsFromProp);
				else
					this.internal_conditions = [MakeNewCondition()];
			},
			newCondition()
			{
				this.internal_conditions.push(MakeNewCondition());
			},
			deleteCondition(condition)
			{
				ModalConfirmDialog("Delete this condition?", "Confirm")
					.then(result =>
					{
						if (result)
						{
							for (let i = 0; i < this.internal_conditions.length; i++)
								if (ConditionsEqual(this.internal_conditions[i], condition))
								{
									this.internal_conditions.splice(i, 1);
									break;
								}
						}
					});
			}
		},
		watch:
		{
			conditionsFromProp()
			{
				this.loadConditionsFromProp();
			},
			internal_conditions:
			{
				handler()
				{
					let query = Object.assign({}, this.$route.query);
					query.scon = JSON.stringify(this.internal_conditions);
					this.$router.replace({ name: this.$route.name, query }).catch(err => { });
				},
				deep: true
			}
		}
	}

	function MakeNewCondition()
	{
		return {
			Enabled: true,
			Operator: "Contains"
		};
	}
	function ConditionsEqual(a, b)
	{
		return a.Enabled === b.Enabled
			&& a.TagKey === b.TagKey
			&& a.Operator === b.Operator
			&& a.Query === b.Query
			&& a.Regex === b.Regex
			&& a.Not === b.Not
			;
	}
</script>

<style scoped>
	.advancedSearchHome
	{
		margin: 10px 20px;
	}

	.inputSection
	{
		margin-bottom: 30px;
	}

		.inputSection .inputLabel
		{
			font-size: 24px;
			font-weight: bold;
			display: block;
			color: #555555;
			margin-bottom: 4px;
		}

	.conditionList
	{
		display: inline-block;
		border: 1px solid #777777;
		border-radius: 8px;
		overflow: hidden;
		min-height: 16px;
		min-width: 300px;
	}

	.conditionList
	{
		background-color: #dddde8;
	}

	.condition:nth-child(2n)
	{
		background-color: #b5b5d8;
	}

	.condition
	{
		padding: 10px 5px 15px 5px;
		border-bottom: 1px solid #777777;
	}

		.condition:last-child
		{
			border-bottom: none;
		}

	.notes
	{
		margin-top: 40px;
		border: 1px solid #777777;
		background-color: #F7F7F7;
	}

	.notesHeading
	{
		padding: 2px 4px;
		background-color: #E0E0E0;
		font-size: 18px;
		font-weight: bold;
	}

	.notesBody li
	{
		margin-bottom: 1em;
		line-height: 1.4em;
	}

	.searchIcon
	{
		fill: currentColor;
		width: 35px;
		height: 35px;
		padding-right: 5px;
	}

	.execSearchButton
	{
		display: inline-flex;
		align-items: center;
		color: #0000ee;
		text-decoration: none;
		font-size: 24px;
		font-weight: bold;
		border: 1px solid #000000;
		padding: 2px 5px 2px 2px;
		margin: 0px 5px;
		background-color: rgba(0,0,0,0.05);
	}

		.execSearchButton:hover
		{
			background-color: rgba(0,0,0,0.085);
		}

		.execSearchButton:active
		{
			background-color: rgba(0,0,0,0.11);
		}
</style>