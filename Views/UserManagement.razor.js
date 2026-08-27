export class UserManagement {
    static extraHeaders = {};
    // Use bigint so we can represent the 64 bit bitfield
    static createUserPermissions = 0b0000n
    
    static hasNumber(inputElement) {
        let isNum = false;
        try {
            BigInt(inputElement.val())
            // because BigInt accepts empty strings for 0n
            if (inputElement.val())
                return true;
        }
        catch { }
        
        return false;
    }
    
    static blinkDsButton(buttonElement) {
        // Blink the button in the stuuupidest way possible
        for (let i = 0; i < 10; i++) {
            if (i % 2 === 0)
                setTimeout(() => { buttonElement.addClass("ds-red") }, i * 250);
            else
                setTimeout(() => { buttonElement.removeClass("ds-red") }, i * 250);
        }
    }
    
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
    
    static async createUser() {
        let button = $("#create-user-button");
        let input = $("#create-user-username");
        let output = $("#create-user-result");

        let reqHeaders = { "Content-Type": "application/json", };
        reqHeaders = Object.assign(reqHeaders, this.extraHeaders);

        let result = await fetch(`/users/create`, {
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
            this.blinkDsButton(button);

            output.parent().removeAttr("style");
            output.text(`Failed! Status code ${result.status}\n${await result.text()}`);
        }
    }
    
    static async findUserByName() {
        let input = $("#find-user-username");
        let button = $("#find-user-name-btn");
        let output = $("#find-user-result");

        let reqHeaders = { };
        reqHeaders = Object.assign(reqHeaders, this.extraHeaders);

        
        let result = await fetch(`/users/findByName?username=${encodeURIComponent(input.val())}`, {
            method: "GET",
            headers: reqHeaders
        });

        output.parent().removeAttr("style");
        if (result.ok) {
            button.removeClass("ds-red");
            button.addClass("ds-green");
            let resJson = await result.json();
            output.text(`Found! Username '${resJson.userName}', ID ${resJson.id}`);
        }
        else {
            button.removeClass("ds-green");
            this.blinkDsButton(button);

            output.parent().removeAttr("style");
            output.text(`Failed! Status code ${result.status}\n${await result.text()}`);
        }
    }
    
    static async findUserById() {
        let input = $("#find-user-id");
        let button = $("#find-user-id-btn");
        let output = $("#find-user-result");
        
        if (!this.hasNumber(input)) {
            output.parent().removeAttr("style");
            output.text(`Psst... the ID is a number...`);
            return;
        }
        
        let reqHeaders = { };
        reqHeaders = Object.assign(reqHeaders, this.extraHeaders);

        let result = await fetch(`/users/findById?id=${input.val()}`, {
            method: "GET",
            headers: reqHeaders
        });
        
        output.parent().removeAttr("style");
        if (result.ok) {
            button.removeClass("ds-red");
            button.addClass("ds-green");
            let resJson = await result.json();
            output.text(`Found! Username '${resJson.userName}', ID ${resJson.id}`);
        }
        else {
            button.removeClass("ds-green");
            this.blinkDsButton(button);

            output.parent().removeAttr("style");
            output.text(`Failed! Status code ${result.status}\n${await result.text()}`);
        }
    }

    static async setUserPassword() {
        let button = $("#set-pwd-btn");
        let idInput = $("#set-pwd-id");
        let pwdInput = $("#set-pwd-password");
        let output = $("#set-pwd-result");
        
        if (!this.hasNumber(idInput)) {
            output.parent().removeAttr("style");
            output.text(`Psst... the ID is a number...`);
            return;
        }
        
        let reqHeaders = { "Content-Type": "application/json", };
        reqHeaders = Object.assign(reqHeaders, this.extraHeaders);
        
        let result = await fetch(`/users/changeUserPassword`, {
            method: "POST",
            body: JSON.stringify({
                TargetId: idInput.val(),
                NewPassword: pwdInput.val()
            }),
            headers: reqHeaders
        });

        if (result.ok)
        {
            // Flash green so there's a success indicator
            button.addClass("ds-green");

            output.parent().removeAttr("style");
            output.text(`Success!`);
        }
        else
        {
            button.removeClass("ds-green");
            this.blinkDsButton(button);

            output.parent().removeAttr("style");
            output.text(`Failed! Status code ${result.status}\n${await result.text()}`);
        }
    }
}

window.UserManagement = UserManagement;
