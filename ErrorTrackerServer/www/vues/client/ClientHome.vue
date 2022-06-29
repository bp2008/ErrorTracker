<template>
	<div :class="{ pageRoot: true, noMargin: !!validatedProjectName }">
		<div v-if="error" class="error">{{error}}</div>
		<div v-else-if="loading" class="loading"><ScaleLoader /> Loading…</div>
		<div v-else-if="validatedProjectName">
			<ProjectDisplay :projectName="validatedProjectName"
							:selectedFolderId="selectedFolderId"
							:openedEventId="openedEventId"
							:selectedEventIds="selectedEventIds"
							:searchArgs="searchArgs"
							:uniqueOnly="uniqueOnly" />
		</div>
		<div v-else>
			<div class="heading">Select a Project</div>
			<div class="bodySection">
				<div v-if="projects && projects.length > 0">
					<div v-for="p in projects" :key="p.Name" class="projectListItem">
						<router-link :to="{ name: 'clientHome', query: { p: p.Name }}">{{p.Name}}</router-link>
						<div class="projectMetadata">{{p.EventCount}} Events, {{p.UniqueEventCount}} Unique</div>
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
			<div class="heading">Tips</div>
			<div class="bodySection">
				<div>
					<ul>
						<li>Most aspects of event management are shared by all users.  Think of it like sharing an email account with others where everyone uses IMAP.  If you move, delete, or recolor an event, it happens for all users.</li>
						<li>Read/Unread state is unique to each user, however if you set this state via a Filter Action, it will affect all current users.</li>
						<li>At this time, changes made by other users may not be seen until you refresh the page.</li>
					</ul>
				</div>
			</div>
			<div class="heading">Login History</div>
			<div class="bodySection">
				<LoginRecordsTable />
			</div>
		</div>
	</div>
</template>

<script>
	import { GetProjectList } from 'appRoot/api/ProjectList';
	import ProjectDisplay from 'appRoot/vues/client/projectdisplay/ProjectDisplay.vue';
	import LoginRecordsTable from 'appRoot/vues/common/loginrecords/LoginRecordsTable.vue';

	export default {
		components: { ProjectDisplay, LoginRecordsTable },
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
			},
			searchArgs: null,
			uniqueOnly: {
				type: Boolean,
				default: false
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
						if (this.projects[i].Name.toLowerCase() === pName)
							return this.projects[i].Name;
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
		margin: 0px 20px;
	}

		.bodySection ul
		{
			padding-left: 20px;
		}

			.bodySection ul li
			{
				margin-bottom: 10px;
			}

				.bodySection ul li:last-child
				{
					margin-bottom: 0px;
				}

	.projectListItem
	{
		margin-bottom: 20px;
	}

		.projectListItem:last-child
		{
			margin-bottom: 0px;
		}

	.projectMetadata
	{
		margin-top: 2px;
		margin-left: 20px;
	}
</style>