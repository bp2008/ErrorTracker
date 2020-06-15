<template>
	<div class="pageRoot">
		<div v-if="(error)" class="error">{{error}}</div>
		<div v-else-if="loading" class="loading"><ScaleLoader /> Loading…</div>
		<div v-else-if="projects" class="">
			<vue-good-table :columns="columns" :rows="projects" @on-row-click="rowClick" />
			<div class="buttonRow">
				<input type="button" value="Add Project" @click="addProject" />
			</div>
		</div>
	</div>
</template>

<script>
	import Editor from 'appRoot/vues/common/editor/Editor.vue';
	import { GetProjectData } from 'appRoot/api/ProjectData';
	import { ModalDialog } from 'appRoot/scripts/ModalDialog';
	import EditProject from 'appRoot/vues/admin/controls/EditProject.vue';
	import AddProject from 'appRoot/vues/admin/controls/AddProject.vue';

	export default {
		components: { Editor },
		data()
		{
			return {
				error: null,
				loading: false,
				projects: null,
				columns: [
					{ label: "Project Name", field: "Name" },
					{ label: "Submit URL", field: "SubmitURL" }
				]
			};
		},
		created()
		{
			this.loadProjects();
		},
		computed:
		{
		},
		methods:
		{
			loadProjects()
			{
				this.loading = true;
				this.projects = null;
				GetProjectData()
					.then(data =>
					{
						if (data.success)
						{
							for (let i = 0; i < data.projects.length; i++)
							{
								data.projects[i].SubmitURL = location.origin + appContext.appPath + "Submit?p=" + data.projects[i].Name + "&k=" + data.projects[i].SubmitKey;
							}
							this.projects = data.projects;
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
			},
			rowClick(params)
			{
				if (this.projects)
					for (let i = 0; i < this.projects.length; i++)
						if (this.projects[i].Name === params.row.Name)
						{
							ModalDialog(EditProject, { project: this.projects[i] })
								.then(result =>
								{
									if (result)
										this.loadProjects();
								});
						}
			},
			addProject()
			{
				ModalDialog(AddProject, {})
					.then(result =>
					{
						if (result)
							this.loadProjects();
					});
			}
		}
	}
</script>

<style scoped>
	.pageRoot
	{
		margin: 8px;
	}

	.loading
	{
		margin-top: 80px;
		text-align: center;
	}

	.error
	{
		color: #FF0000;
		font-weight: bold;
	}

	.buttonRow
	{
		margin-top: 30px;
		border-top: 1px solid #AAAAAA;
		padding-top: 10px;
	}
</style>