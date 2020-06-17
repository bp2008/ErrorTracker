<template>
	<div :class="{ pageRoot: true, noMargin: validatedProjectName }">
		<div v-if="(error)" class="error">{{error}}</div>
		<div v-else-if="loading" class="loading"><ScaleLoader /> Loading…</div>
		<div v-else-if="validatedProjectName">
			<ProjectDisplay :projectName="validatedProjectName"
							:selectedFolderId="selectedFolderId"
							:openedEventId="openedEventId"
							:selectedEventIds="selectedEventIds" />
		</div>
		<div v-else>
			<div class="heading">Select a Project</div>
			<div class="bodySection">
				<div v-if="projects && projects.length > 0">
					<div v-for="p in projects" :key="p" class="projectListItem">
						<router-link :to="{ name: 'clientHome', query: { p: p }}">{{p}}</router-link>
					</div>
				</div>
				<div v-else>
					No projects are accessible by your user account.
				</div>
			</div>
			<div class="heading">Manage User Account</div>
			<div class="bodySection">
				<router-link :to="{ name: 'changePassword' }">Change Password</router-link>
			</div>
			<div class="heading">Login History</div>
			<div class="bodySection">
				<div>Under Construction</div>
			</div>
		</div>
	</div>
</template>

<script>
	import { GetProjectList } from 'appRoot/api/ProjectList';
	import ProjectDisplay from 'appRoot/vues/client/projectdisplay/ProjectDisplay.vue';

	export default {
		components: { ProjectDisplay },
		props:
		{
			projectName: {
				type: String,
				default: ""
			},
			selectedFolderId: {
				type: Number,
				default: 0
			},
			openedEventId: {
				default: null
			},
			selectedEventIds: {
				type: String,
				default: ""
			}
		},
		data()
		{
			return {
				error: null,
				loading: false,
				projects: null
			};
		},
		created()
		{
			this.loadProjectList();
		},
		computed:
		{
			validatedProjectName()
			{
				if (this.projects)
				{
					let pName = this.projectName.toLowerCase();
					for (let i = 0; i < this.projects.length; i++)
					{
						if (this.projects[i].toLowerCase() === pName)
							return this.projects[i];
					}
				}
				return null;
			}
		},
		methods:
		{
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
		},
		watch:
		{
		}
	}
</script>

<style scoped>
	.pageRoot
	{
		margin: 8px;
	}

		.pageRoot.noMargin
		{
			margin: 0px;
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

	.heading
	{
		font-size: 20px;
		border-bottom: 1px solid #000000;
		margin-bottom: 10px;
		margin-top: 30px;
		max-width: 400px;
	}

		.heading:first-child
		{
			margin-top: 0px;
		}

	.bodySection
	{
		margin-left: 20px;
	}
</style>