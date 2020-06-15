<template>
	<div class="dialogContainer">
		<transition-group :name="transitionName">
			<component v-for="c in components"
					   :is="c.component"
					   v-bind="c.props"
					   :key="c.key"
					   @close="function(dialogResult) { closeRequested(c.key, dialogResult) }" />
		</transition-group>
	</div>
</template>

<script>
	// One instance of this component should exist near the end of the root vue component.  All modal dialogs will be children of this.
	import { RegisterModalDialogContainer, UnregisterModalDialogContainer } from 'appRoot/scripts/ModalDialog.js';

	let uniqueComponentCounter = 0;
	export default {
		props:
		{
			name: {
				type: String,
				required: true
			},
			transitionName: {
				type: String,
				default: ""
			}
		},
		data()
		{
			return {
				components: []
			};
		},
		created()
		{
			RegisterModalDialogContainer(this);
		},
		beforeDestroy()
		{
			while (this.components.length > 0)
				this.components.pop().onDialogClose(false);
		},
		destroyed()
		{
			UnregisterModalDialogContainer(this);
		},
		methods:
		{
			CreateDialog(component, props, onDialogClose)
			{
				let c = { component, props, onDialogClose, key: uniqueComponentCounter++ };
				this.components.push(c);
				return c.key;
			},
			closeRequested(key, dialogResult)
			{
				for (let i = 0; i < this.components.length; i++)
				{
					if (this.components[i].key === key)
					{
						let c = this.components[i];
						this.components.splice(i, 1);
						c.onDialogClose(dialogResult);
						return;
					}
				}
			},
			CloseAllDialogs()
			{
				for (let i = 0; i < this.components.length; i++)
				{
					let c = this.components[i];
					this.components.splice(i, 1);
					c.onDialogClose(false);
				}
			}
		}
	}
</script>