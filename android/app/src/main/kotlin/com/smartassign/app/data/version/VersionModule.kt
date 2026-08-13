package com.smartassign.app.data.version

import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class VersionModule {
    @Binds
    @Singleton
    abstract fun verificadorVersionRepositorio(impl: VerificadorVersionRepositorioImpl): VerificadorVersionRepositorio
}
