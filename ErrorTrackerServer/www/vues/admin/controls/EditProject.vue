<template>
	<div class="editProjectRoot">
		<div class="title">Project: {{project.Name}}</div>
		<div class="dataRow">Current event submission URL:</div>
		<div><code class="url">{{project.SubmitURL}}</code></div>
		<div>
			<input type="button" value="Replace Submission Key" @click="ReplaceSubmitKey" title="Replaces the submission key with a new one (requires confirmation)" />
			<input type="button" value="Copy Submission URL" @click="CopySubmitUrl" />
		</div>
		<div class="dataRow">Max Event Age (Days): <input type="number" v-model="maxEventAgeDays" min="0" max="36500" /> (0 to disable automatic Event deletion)</div>
		<div class="buttonRow">
			<input type="button" value="Commit Changes" @click="Commit" />
			<input type="button" value="Delete Project" @click="Remove" />
			<input ref="cancelBtn" type="button" value="Cancel" @click="Cancel" />
		</div>
	</div>
</template>

<script>
	import { RemoveProject, ReplaceSubmitKey, UpdateProject } from 'appRoot/api/ProjectData';
	import { ModalConfirmDialog } from 'appRoot/scripts/ModalDialog';
	import { CopyToClipboard } from 'appRoot/scripts/Util';

	export default {
		components: {},
		props:
		{
			project: {
				type: Object,
				required: true
			}
		},
		data()
		{
			return {
				maxEventAgeDays: 0
			};
		},
		created()
		{
			this.maxEventAgeDays = this.project.MaxEventAgeDays;
		},
		mounted()
		{
		},
		computed:
		{
		},
		methods:
		{
			SetFocus()
			{
				if (this.$refs.cancelBtn)
					this.$refs.cancelBtn.focus();
			},
			DefaultClose()
			{
				this.Cancel();
			},
			Cancel()
			{
				this.$emit("close", false);
			},
			Remove()
			{
				ModalConfirmDialog("Are you sure you want to delete this project?\n\nThe database for this project will remain on disk and can be accessed again by simply re-creating the project.", "Confirm")
					.then(result =>
					{
						if (result)
						{
							RemoveProject(this.project.Name)
								.then(data =>
								{
									if (data.success)
										this.$emit("close", true);
									else
										toaster.error(data.error);
								})
								.catch(err =>
								{
									toaster.error(err);
								});
						}
					});
			},
			Commit()
			{
				UpdateProject(this.project.Name, this.maxEventAgeDays)
					.then(data =>
					{
						if (data.success)
							this.$emit("close", true);
						else
							toaster.error(data.error);
					})
					.catch(err =>
					{
						toaster.error(err);
					});
			},
			ReplaceSubmitKey()
			{
				ModalConfirmDialog("Are you sure you want to change the submission key for this project?\n\nAny apps using the current submission key will need to be updated in order for event reporting to be restored.\n\nThe Edit Project dialog will be closed and any other configuration changes will be lost.", "Confirm")
					.then(result =>
					{
						if (result)
						{
							ReplaceSubmitKey(this.project.Name)
								.then(data =>
								{
									if (data.success)
										this.$emit("close", true);
									else
										toaster.error(data.error);
								})
								.catch(err =>
								{
									toaster.error(err);
								});
						}
					});
			},
			CopySubmitUrl()
			{
				if (this.project.SubmitURL)
				{
					CopyToClipboard(this.project.SubmitURL);
					toaster.success("Copied!");
				}
				else
					toaster.error("Application Error. No submission URL was found for this project.");
			}
		},
		beforeDestroy()
		{
		}
	};
</script>

<style scoped>
	.editProjectRoot
	{
		margin: 10px;
		min-width: 260px;
	}

	.title
	{
		border-bottom: 1px solid #000000;
		margin-bottom: 10px;
		font-size: 20px;
	}

	.url
	{
		display: inline-block;
		border: 1px dotted #AAAAAA;
		background-color: #dff1df;
		padding: 2px 4px;
		word-break: break-word;
		font-family: Consolas, monospace;
		margin: 5px 0px;
	}

	.buttonRow
	{
		margin-top: 20px;
	}

	.dataRow
	{
		margin-top: 10px;
	}
</style>