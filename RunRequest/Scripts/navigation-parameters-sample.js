var navigationParametersSample = {
    links: [
        {
            linkText: "Home",
            actionName: "Index",
            controllerName: "Home"
        },
        {
            linkText: "About",
            actionName: "About",
            controllerName: "Home",
        },
        {
            linkText: "Contact",
            actionName: "Contact",
            controllerName: "Home",
        }
    ],
    search: {
        enabled: true,
        method: "POST",
        postAction: "/Home/GlobalSearch"
    }
}