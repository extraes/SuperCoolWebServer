export class UserManagement {
    static extraHeaders = {};
    // Use bigint so we can represent the 64 bit bitfield
    static createUserPermissions = 0b0000n
    static createUserTogglePermission(value) {
        value = BigInt(value);
        // If you're someone who's not me and is reading this, you may be wondering "If I toggle admin, and then
        // deselect one thing, doesn't that mess up all the other bits?"
        // It does. I just don't care. Because I make the server discard all bits that aren't in use.
        // Because it was easier to do that than to try to find out how I could get that bitmask in here in JS.
        if (value !== -1n)
            this.createUserPermissions ^= value;
        else if (this.createUserPermissions === -1n)
            this.createUserPermissions = 0n;
        else 
            this.createUserPermissions = -1n;
        
        let buttons = $(".create-user-permission-button");
        for (let i = 0; i < buttons.length; i++) {
            let button = buttons[i];
            let buttonValue = button.getAttribute("value")
            let buttonNum = BigInt(buttonValue);
            
            if ((this.createUserPermissions & buttonNum) === buttonNum)
            {
                button.classList.add("ds-green")
                button.classList.remove("ds-red")
            }
            else
            {
                button.classList.remove("ds-green")
                button.classList.add("ds-red")
            }
        }
        
    }
    static async CreateUser() {
        let button = $("#create-user-button");
        let input = $("#create-user-username");
        let output = $("#create-user-result");

        let reqHeaders = { "Content-Type": "application/json", };
        reqHeaders = Object.assign(reqHeaders, this.extraHeaders);

        let result = await fetch(`users/create`, {
            method: "POST",
            body: JSON.stringify({
                Username: input.val(),
                PermissionsInteger: this.createUserPermissions.toString()
            }),
            headers: reqHeaders
        });
        
        if (result.ok)
        {
            // Flash green so there's a success indicator
            button.addClass("ds-green");
            let resJson = await result.json();

            output.parent().removeAttr("style");
            output.text(`Success! Log in with the password\n${resJson.password}\nand be sure to change it when you log in!`);
        }
        else
        {
            // Blink the login button in the stuuupidest way possible
            for (let i = 0; i < 10; i++) {
                if (i % 2 === 0)
                    setTimeout(() => { button.addClass("ds-red") }, i * 250);
                else
                    setTimeout(() => { button.removeClass("ds-red") }, i * 250);
            }

            output.parent().removeAttr("style");
            output.text(`Failed! Status code ${result.status}\n${await result.text()}`);
        }
    }
}

window.UserManagement = UserManagement;