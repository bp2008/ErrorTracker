<template>
	<div class="selectProjectDialogRoot" ref="dialogRoot" role="dialog" aria-labelledby="textInputMsgTitle" aria-describedby="textInputMsg">
		<div class="titleBar">
			<div id="textInputMsgTitle" class="title" role="alert">
				<template v-if="title">
					{{title}}
				</template>
				<template v-else>
					Select a Project
				</template>
			</div>
		</div>
		<div class="dialogBody">
			<div v-if="error" class="error">{{error}}</div>
			<div v-else-if="loading" class="loading"><ScaleLoader /> Loading…</div>
			<div v-else-if="projects && projects.length > 0">
				<div v-for="p in projects" :key="p.Name" class="projectListItem">
					<a role="button" @click="projectClicked(p)" v-bind:class="{ projectButton: true, selectedProjectButton: currentSelectedProjectName === p.Name}">
						{{p.Name}}
					</a>
				</div>
			</div>
			<div v-else>
				No projects are accessible by your user account.
			</div>
		</div>
		<div class="buttons">
			<div class="dialogButton okButton" tabindex="0" @click="okClicked" @keydown.space.enter.prevent="okClicked">
				OK
			</div>
			<div class="dialogButton cancelButton" tabindex="0" @click="cancelClicked" @keydown.space.enter.prevent="cancelClicked">
				Cancel
			</div>
		</div>
	</div>
</template>

<script>
	import { GetProjectList } from 'appRoot/api/ProjectList';

	export default {
		components: {},
		props:
		{
			selectedProjectName: {
				type: String,
				default: ""
			},
			title: {
				type: String,
				default: ""
			}
		},
		data()
		{
			return {
				error: null,
				loading: false,
				projects: null,
				currentSelectedProjectName: this.selectedProjectName
			};
		},
		created()
		{
			this.loadProjectList();
		},
		mounted()
		{
			this.SetFocus();
		},
		computed:
		{
		},
		methods:
		{
			SetFocus()
			{
				if (this.$refs.dialogRoot)
					this.$refs.dialogRoot.focus();
			},
			DefaultClose()
			{
				this.cancelClicked();
			},
			okClicked()
			{
				this.$emit("close", this.currentSelectedProjectName); // project name can be empty if no project was selected
			},
			cancelClicked()
			{
				this.currentSelectedProjectName = "";
				this.$emit("close", "");
			},
			projectClicked(p)
			{
				this.currentSelectedProjectName = p.Name;
			},
			loadProjectList()
			{
				this.loading = true;
				this.projects = null;
				this.error = null;
				GetProjectList()
					.then(data =>
					{
						if (data.success)
							this.projects = data.projects;
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
	.selectProjectDialogRoot
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

	.projectListItem
	{
		margin: 10px 5px;
	}

	.projectButton
	{
		color: #0000ee;
		text-decoration: underline;
		border: 2px solid transparent;
		padding: 2px 5px;
	}

		.projectButton:hover
		{
			border: 2px solid #CCCCCC;
		}

	.selectedProjectButton
	{
		border: 2px solid #0066FF;
	}

		.selectedProjectButton:hover
		{
			border: 2px solid #3399FF;
		}

	.loading
	{
		text-align: center;
	}
</style>