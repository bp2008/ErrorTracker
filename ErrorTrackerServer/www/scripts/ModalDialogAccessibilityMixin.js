/// This mixin is designed to keep the input focus inside the dialog box (for example, when the user presses tab).

/// To use this mixin, you must implement the method "SetFocus()", which should call
/// focus() on the first DOM element in your dialog box that is capable of receiving focus.

export default { /* ModalDialogAccessibilityMixin */
	data()
	{
		return {
			oldFocus: null
		};
	},
	methods:
	{
		LostFocus(event)
		{
			this.CallSetFocus();
		},
		CallSetFocus()
		{
			if (typeof this.SetFocus === "function")
				this.SetFocus();
			else if (process.env.NODE_ENV !== 'production')
			{
				console.error("Component implementing ModalDialogAccessibilityMixin does not implement SetFocus method.", this);
				alert("Component implementing ModalDialogAccessibilityMixin does not implement SetFocus method.");
			}
		},
		PreventDefault(event)
		{
			event.preventDefault();
			return false;
		}
	},
	mounted()
	{
		this.$data.oldFocus = document.activeElement;
		this.CallSetFocus();
		let mainLayout = window.mainLayoutRef || document.getElementById("appContentRoot");
		if (mainLayout)
			mainLayout.addEventListener("focusin", this.LostFocus, true);
	},
	beforeDestroy()
	{
		let mainLayout = window.mainLayoutRef || document.getElementById("appContentRoot");
		if (mainLayout)
			window.mainLayoutRef.removeEventListener("focusin", this.LostFocus, true);
		if (this.$data.oldFocus && this.$data.oldFocus.focus)
			this.$data.oldFocus.focus();
	}
};