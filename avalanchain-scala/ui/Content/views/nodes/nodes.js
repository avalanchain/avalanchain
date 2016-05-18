(function () {
    'use strict';
    var controllerId = 'Nodes';
    angular.module('avalanchain').controller(controllerId, ['common','$scope', 'dataservice', Nodes]);

    function Nodes(common, $scope, dataservice) {

        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);

        this.info = 'Nodes';
        this.helloText = 'Welcome in Avalanchain';
        this.descriptionText = 'CASCADING REACTIVE BLOCKCHAINS';

        $scope.isUp = false;
        dataservice.getNodes($scope)

        $scope.addNode = function () {
          $scope.isUp = true;
          dataservice.addNode();
        }
        activate();

        function activate() {
            common.activateController([], controllerId)
                .then(function () { log('Activated Nodes') });//log('Activated Admin View');
        }
    };


})();
