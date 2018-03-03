(function () {
    'use strict';
    var controllerId = 'createassets';
    angular.module('avalanchain').controller(controllerId, ['common', 'dataservice', '$scope', '$uibModal', '$rootScope', '$state', createassets]);

    function createassets(common, dataservice, $scope, $uibModal, $rootScope, $state) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.info = 'Dashboard Investor';
        // var currencies = dataservice.commondata().currencies();
        // var percentage = dataservice.commondata().percentage();
        // $scope.currlist = [];
        vm.spinOption = {
            min: 0,
            max: 10000000000,
            step: 1,
            decimals: 0,
            boostat: 5,
            maxboostedstep: 100000,
        };
        vm.createAsset = function (asset) {

            dataservice.addAsset(asset)
            // .then(function(data) {

            // });
            log('Asset: ' + asset.name + ' created succesfully!')
            $state.go('dashboards.dashboard');


        };

        activate();

        function activate() {
            common.activateController([], controllerId)
                .then(function () {
                    log('Activated Assets Creation')
                }); //log('Activated Admin View');
        }

        $scope.$watch('yourItems', function (newVal, oldVal) {
            if (newVal !== oldVal) {
                // render charts
            }
        });
        var dataprev = [];

        function getData() {

            // if (dataF.length) {
            //     dataF = dataF.slice(1);
            // }

            // // zip the generated y values with the x values
            // dataF.push([dataF.length, $scope.quoka]);
            // var res = [];
            // for (var i = 0; i < dataF.length; ++i) {
            //     res.push([i, dataF[i][1]]);
            // }
            // //dataF = res;
            // return res;
        }

        function getMessageCount() {
            // return dataservice.getYData().then(function (data) {
            //     $scope.datayahoo = addStatus(data.data.query.results.rate);
            //     $scope.quoka = dataservice.getQuoka($scope.datayahoo);
            //     $scope.flotLineAreaData[0].data = getData();
            // });
        }
    };


})();