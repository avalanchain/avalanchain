
(function() {
    'use strict';
    var controllerId = 'chains';
    angular.module('avalanchain').controller(controllerId, ['common', '$uibModal', 'dataservice', 'jwtservice', '$rootScope','$timeout', chains]);

    function chains(common, $uibModal, dataservice, jwtservice, $rootScope, $timeout) {
        var getLogFn = common.logger.getLogFn;
        var log = getLogFn(controllerId);
        var vm = this;
        vm.encode = 'true';
        // vm.decode = !vm.encode;
        vm.maxSize = 5;
        vm.totalItems = [];
        vm.page = 1;


        vm.Fork = function (parent) {
            var m = new Mnemonic(96);
            $rootScope.modal = {};
            $rootScope.modal.parent = parent;
            $rootScope.modal.position = 0;
            $rootScope.modal.ok =function () {
                return dataservice.newUser().then(function (data) {
                    // $rootScope.$emit('updateAccounts');
                    return 200;
                });
            };
            $rootScope.modal.canel =function () {
                // dataservice.newUser().then(function (data) {
                //     $rootScope.$emit('updateAccounts');
                // });
            };
            var modalInstance = $uibModal.open({
                templateUrl: '/app/views/chains/fork.html',
                controller: modalCtrl
            });
        };
        var code  = 'function Function(params) {\n\tvar said= \'...\';\n\tvar helloWorld = function() { \n\tvar said = "Hello world!";\n\t}\n}'
        vm.editorOptions = {
            lineNumbers: true,
            matchBrackets: true,
            styleActiveLine: true,
            autofocus:true
        };
        vm.Derived = function (parent) {
            var m = new Mnemonic(96);
            $rootScope.modal = {};
            $rootScope.modal.parent = parent;
            $rootScope.modal.code = code;
            $rootScope.modal.editorOptions =  vm.editorOptions;
            $rootScope.modal.ok =function () {
                return dataservice.newUser().then(function (data) {
                    // $rootScope.$emit('updateAccounts');
                    return 200;
                });
            };
            $rootScope.modal.canel =function () {
                // dataservice.newUser().then(function (data) {
                //     $rootScope.$emit('updateAccounts');
                // });
            };
            $rootScope.modal.init = function () {
                $timeout(function () {
                    $rootScope.modal.refresh = true;
                }, 200);

            };
            var modalInstance = $uibModal.open({
                templateUrl: '/app/views/chains/derived.html',
                controller: modalCtrl
            });
        };
        vm.test = function() {
            var tt = vm.encode;
              var tt = vm.decode;
        }
        activate();

        function activate() {
            common.activateController([getData()], controllerId)
                .then(function() {
                    log('Activated Chains')
                }); //log('Activated Admin View');
        }

        function getData() {
            dataservice.getData().then(function(data) {
                vm.data = data;
                vm.streams = vm.data.streams;
                vm.tokens = vm.streams.map(function(stream) {
                  var jwTDecode = jwtservice.generateJWT(stream);
                  var token = {
                    token : jwTDecode,
                    verified : jwtservice.verifyJWT(jwTDecode, stream.publicKey)
                  }
                    return token;
                });
                //var jwS = jwtservice.generateJWS(vm.streams[0]);
                // var jwTDecode = jwtservice.generateJWT(vm.streams[0]);
                // var isValidjwt = jwtservice.verifyJWS(jwTDecode);
                //
                // var jwtencode = jwtservice.getJWT(jwTDecode);
            });

        }

    };


})();
