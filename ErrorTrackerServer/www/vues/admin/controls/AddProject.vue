<template>
	<div class="addProjectRoot">
		<div class="title">Add New Project</div>
		<div>Project Name:</div>
		<div><input type="text" v-model="projectName" @keypress.enter.prevent="Add" /></div>
		<div class="buttonRow">
			<input type="button" value="Add Project" @click="Add" />
			<input ref="cancelBtn" type="button" value="Cancel" @click="Cancel" />
		</div>
	</div>
</template>

<script>
	import { AddProject } from 'appRoot/api/ProjectData';

	export default {
		data()
		{
			return {
				projectName: ""
			};
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
			Add()
			{
				AddProject(this.projectName)
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
		}
	};
</script>

<style scoped>
	.addProjectRoot
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

	.buttonRow
	{
		margin-top: 10px;
	}
</style>