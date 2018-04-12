/**
 * @ngdoc controller
 * @name Umbraco.Editors.ContentBlueprint.CreateController
 * @function
 *
 * @description
 * The controller for creating content blueprints
 */
function ContentBlueprintCreateController($scope, $location, contentTypeResource, navigationService, appState) {

    var vm = this;
    var node = $scope.dialogOptions.currentNode;
    var section = appState.getSectionState("currentSection");

    vm.createBlueprint = createBlueprint;

    function onInit() {

        vm.loading = true;

        contentTypeResource.getAll()
            .then(function (documentTypes) {
                vm.documentTypes = documentTypes;
                vm.loading = false;
            });
    }

    function createBlueprint(documentType) {
        $location.path("/" + section + "/contentBlueprints/edit/" + node.id).search("create", "true").search("doctype", documentType.alias);
        navigationService.hideMenu();
    }

    onInit();
}

angular.module("umbraco").controller("Umbraco.Editors.ContentBlueprint.CreateController", ContentBlueprintCreateController);
