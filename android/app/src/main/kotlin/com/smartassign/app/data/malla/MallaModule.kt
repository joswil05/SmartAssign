package com.smartassign.app.data.malla

import dagger.Binds
import dagger.Module
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
abstract class MallaModule {
    @Binds
    @Singleton
    abstract fun mallaRepositorio(impl: MallaRepositorioImpl): MallaRepositorio
}
